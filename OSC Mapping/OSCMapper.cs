using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;

namespace ReSounding;
public static class OSCMapper
{
    // Concurrent dictionary since we do lazy caching for the OSC lookups that needs to be thread safe
    private static readonly ConcurrentDictionary<Type, FrozenDictionary<string, Action<object, object[]>>> fieldCaches = new();

    // Scan T for conversion methods. Methods must have only 1 parameter of type object[]
    private static FrozenDictionary<Type, MethodInfo>? converters;

    /// <summary>
    /// Registers a class of converters for custom type interpretations.
    /// </summary>
    /// <param name="t"></param>
    public static void RegisterConverters(Type t)
    {
        converters = t
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(object[]))
            .ToFrozenDictionary(k => k.ReturnType);
    }

    /// <summary>
    /// Attempts to map an OSC message's arguments onto a class by OSC address.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="address"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool TryMapOSC(object obj, string address, params object[] data)
    {
        Type objType = obj.GetType();

        // Check if the type exists in our lookup.
        // If not, attempt to generate a lookup for the OSC-mapped fields
        if (!fieldCaches.TryGetValue(objType, out var lookup))
        {
            lookup = objType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) // Get the members
                .Select(mbr => (mbr, attr: mbr.GetCustomAttribute<OSCMapAttribute>())) // Get the attribute & pass it along
                .Where(m => m.attr != null)                                            // Make sure the attribute isn't null
                .ToFrozenDictionary(a => a.attr!.Path, a => MapMember(a.mbr));         // Store the lookup in a frozen dict for efficiency
            
            fieldCaches.TryAdd(objType, lookup);                                       // Add the lookup to the type lookup dict
        }

        if (lookup.TryGetValue(address, out var action)) // See if the OSC path has an accessor
        {
            action.Invoke(obj, data);                    // Invoke the accessor with the data we want to set
            return true;
        }
        return false;
    }

    /// <summary>
    /// Takes either a field or a property member and creates an accessor delegate for it.
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    internal static Action<object, object[]> MapMember(MemberInfo member)
    {
        // Ensure that the member is actually a property or a field
        Type? memberType =
            (member as FieldInfo)?.FieldType ??
            (member as PropertyInfo)?.PropertyType;
        
        if (member.DeclaringType == null || memberType == null)
            throw new NullReferenceException($"Something's gone terribly wrong and MapMember() was passed an incompatible type! Type was: {member.DeclaringType?.ToString() ?? "null"}");

        // Define two parameters; one for a mapped object, and another for the object array to pass
        var targ = Expression.Parameter(typeof(object), "target");
        var obj = Expression.Parameter(typeof(object[]), "object");


        var targCasted = Expression.Convert(targ, member.DeclaringType); // Cast "target" to the declaring type since we know it
        
        MethodInfo? conv = null;
        converters?.TryGetValue(memberType, out conv); // Try to get a converter
        var first = Expression.ArrayAccess(obj, Expression.Constant(0)); // object[0], just in case.


        // If a converter is found, use it to convert the array, otherwise attempt to convert object[0] the good ol'-fashioned way.
        Expression convert = 
            conv != null ?
                Expression.Call(null, conv, obj) :
                Expression.Convert(first, memberType);


        Expression targAccessor = Expression.MakeMemberAccess(targCasted, member); // Make an accessor for the field or property
        var objAssign = Expression.Assign(targAccessor, convert);                  // Use the accessor to assign a value to the member


        // Finally, compile the lambda and return.
        return Expression.Lambda<Action<object, object[]>>(objAssign, targ, obj).Compile();
    }
}
