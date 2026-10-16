using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;

// Compatibility for the existing protocol code's untyped JSON tree. This is a JSON
// adapter, not an emulation of any Windows UI or platform API.
namespace System.Web.Script.Serialization
{
    public sealed class JavaScriptSerializer
    {
        public int MaxJsonLength {get;set;}=2097152;
        public string Serialize(object value)
        {
            string result=JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
            if(result.Length>MaxJsonLength)throw new ArgumentException("JSON exceeds its length limit.");
            return result;
        }
        public T Deserialize<T>(string value)
        {
            Validate(value);
            if(typeof(T)==typeof(object) || typeof(T)==typeof(Dictionary<string,object>))
                return (T)Parse(value,true);
            if(typeof(T)==typeof(object[]))return (T)DeserializeObject(value);
            return JsonSerializer.Deserialize<T>(value,new JsonSerializerOptions{IncludeFields=true,PropertyNameCaseInsensitive=true});
        }
        public object DeserializeObject(string value)
        {
            Validate(value);
            return Parse(value,false);
        }
        static object Parse(string value,bool arrayLists)
        {
            try {using(var json=JsonDocument.Parse(value))return ConvertElement(json.RootElement,arrayLists);}
            catch(JsonException error){throw new ArgumentException("Invalid JSON.",error);}
        }
        void Validate(string value)
        {
            if(value==null)throw new ArgumentNullException("value");
            if(value.Length>MaxJsonLength)throw new ArgumentException("JSON exceeds its length limit.");
        }
        static object ConvertElement(JsonElement element,bool arrayLists)
        {
            switch(element.ValueKind)
            {
                case JsonValueKind.Object:
                    var result=new Dictionary<string,object>();
                    foreach(var property in element.EnumerateObject())result[property.Name]=ConvertElement(property.Value,arrayLists);
                    return result;
                case JsonValueKind.Array:
                    var array=new List<object>();foreach(var item in element.EnumerateArray())array.Add(ConvertElement(item,arrayLists));
                    return arrayLists?(object)new ArrayList(array):array.ToArray();
                case JsonValueKind.String:return element.GetString();
                case JsonValueKind.True:return true;
                case JsonValueKind.False:return false;
                case JsonValueKind.Number:
                    int integer;if(element.TryGetInt32(out integer))return integer;
                    long number;if(element.TryGetInt64(out number))return number;
                    decimal precise;if(element.TryGetDecimal(out precise))return precise;
                    return element.GetDouble();
                default:return null;
            }
        }
    }
}
