# Re-Sounding: For your simple address-mapping needs!

Re-Sounding is a simple module that primarily is tailored to work with Rug.Osc. It lets you define attributes in your class and map an array of objects to them.

# Usage


In your class, simply annotate your fields or properties with the following:

```csharp
public class MyClass
{
    [OSCMap("/my/field/here")]
    public float myField;

    [OSCMap("/my/property/here")]
    public int myProperty { get; set; }

    [OSCMap("/my/data/here")]
    public MyDataType myData;
}
```

Then call `OSCMapper.TryMapOSC(object target, string Address, params object[] data)` with object being your class.

e.x.
```csharp
MyClass mine = new();
float myValue = 3f;

OSCMapper.TryMapOSC(mine, "/my/field/here", myValue);

Console.WriteLine(mine.myField); // Prints "3"
```

You can also pass in an object array as such (particularly useful when using Rug.Osc):

```csharp
object[] data = new[] { 1f, 3f, 5f }

OSCMapper.TryMapOSC(mine, "my/path/here", data);
```

If you need to parse multiple arguments - even of varying types - you can simply either use or define your own class or struct, and register a static converter class with the OSC mapper before mapping.

```csharp
public struct MyDataType
{
    public int FirstInt;
    public float SecondFloat;
    public bool ThirdBool;
}
```

Then, define a static class with conversion methods, must have 1 argument of type object[] and return a valid type.

```csharp
public static class MyConversions
{
    // Define conversion method, convert to your data type how you desire. Function can be named anything.
    public MyDataType Convert(object[] data)
    {
        // Make sure the data array contains the types you actually want!! Otherwise this will fail.
        return new() { FirstInt = data[0], SecondFloat = data[1], ThirdBool = data[2] }
    }
}
```

Lastly, register your static converter class with the OSCMapper using `OSCMapper.RegisterConverters(Type t)`

```csharp
OSCMapper.RegisterConverters(typeof(MyConversions))

OSCMapper.TryMapOSC(mine, "/my/data/here", data)
```

