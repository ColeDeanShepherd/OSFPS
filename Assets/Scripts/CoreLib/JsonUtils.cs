using System;
using System.Reflection;
using System.Text;

public static class JsonUtils
{
    public static string ToPrettyJson(object obj)
    {
        var stringBuilder = new StringBuilder();
        ToPrettyJson(obj, stringBuilder, 0);

        return stringBuilder.ToString();
    }
    public static void ToPrettyJson(object obj, StringBuilder stringBuilder, uint indentationLevel)
    {
        const uint spacesPerTab = 4;
        Action appendIndentation = () => stringBuilder.Append(' ', (int)(spacesPerTab * indentationLevel));

        if (obj == null)
        {
            stringBuilder.Append("null");
            return;
        }

        var objType = obj.GetType();

        if (objType.IsPrimitive || objType.IsEnum || (objType.Equals(typeof(decimal))))
        {
            stringBuilder.Append(obj.ToString());
        }
        else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(objType))
        {
            stringBuilder.AppendLine("[");
            indentationLevel++;

            var collection = (System.Collections.ICollection)obj;
            var elementIndex = 0;

            foreach (var element in collection)
            {
                appendIndentation();
                ToPrettyJson(element, stringBuilder, indentationLevel);

                if (elementIndex < (collection.Count - 1))
                {
                    stringBuilder.AppendLine(",");
                }
                else
                {
                    stringBuilder.AppendLine();
                }

                elementIndex++;
            }

            indentationLevel--;
            appendIndentation();
            stringBuilder.AppendLine("]");
        }
        else if (objType.IsValueType || objType.IsClass)
        {
            stringBuilder.AppendLine("{");
            indentationLevel++;

            foreach (var fieldInfo in objType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                appendIndentation();
                stringBuilder.Append($"\"{fieldInfo.Name}\": ");

                var fieldValue = fieldInfo.GetValue(obj);
                ToPrettyJson(fieldValue, stringBuilder, indentationLevel);

                stringBuilder.AppendLine();
            }

            indentationLevel--;
            appendIndentation();
            stringBuilder.AppendLine("}");
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}