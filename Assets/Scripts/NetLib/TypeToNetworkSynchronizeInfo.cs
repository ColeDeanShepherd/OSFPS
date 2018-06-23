using System.Reflection;

namespace NetworkLibrary
{
    public struct TypeToNetworkSynchronizeInfo
    {
        public FieldInfo[] FieldsToSynchronize;
        public PropertyInfo[] PropertiesToSynchronize;

        public int NumberOfThingsToSynchronize
        {
            get
            {
                return FieldsToSynchronize.Length + PropertiesToSynchronize.Length;
            }
        }
    }
}