﻿using JSONAPI.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JSONAPI.Core
{
    public class ModelManager : IModelManager
    {
        public ModelManager() {
            _pluralizationService = new PluralizationService();
        }

        public ModelManager(IPluralizationService pluralizationService)
        {
            _pluralizationService = pluralizationService;
        }

        private IPluralizationService _pluralizationService = null;
        public IPluralizationService PluralizationService
        {
            get
            {
                return _pluralizationService;
            }
        }

        #region Cache storage

        private Lazy<Dictionary<Type, PropertyInfo>> _idProperties
            = new Lazy<Dictionary<Type,PropertyInfo>>(
                () => new Dictionary<Type, PropertyInfo>()
            );

        private Lazy<Dictionary<Type, Dictionary<string, PropertyInfo>>> _propertyMaps
            = new Lazy<Dictionary<Type, Dictionary<string, PropertyInfo>>>(
                () => new Dictionary<Type, Dictionary<string, PropertyInfo>>()
            );

        private Lazy<Dictionary<Type, string>> _jsonKeysForType
            = new Lazy<Dictionary<Type, string>>(
                () => new Dictionary<Type, string>()
            );

        #endregion

        #region Id property determination

        public PropertyInfo GetIdProperty(Type type)
        {
            PropertyInfo idprop = null;

            var idPropCache = _idProperties.Value;

            lock (idPropCache)
            {
                if (idPropCache.TryGetValue(type, out idprop)) return idprop;

                //TODO: Enable attribute-based determination

                idprop = type.GetProperty("Id");

                if (idprop == null)
                    throw new InvalidOperationException(String.Format("Unable to determine Id property for type {0}", type));

                idPropCache.Add(type, idprop);
            }

            return idprop;
        }

        #endregion

        #region Property Maps

        protected IDictionary<string, PropertyInfo> GetPropertyMap(Type type) //FIXME: Will become protected
        {
            Dictionary<string, PropertyInfo> propMap = null;

            var propMapCache = _propertyMaps.Value;

            lock (propMapCache)
            {
                if (propMapCache.TryGetValue(type, out propMap)) return propMap;

                propMap = new Dictionary<string, PropertyInfo>();
                PropertyInfo[] props = type.GetProperties();
                foreach (PropertyInfo prop in props)
                {
                    propMap[GetJsonKeyForProperty(prop)] = prop;
                }

                propMapCache.Add(type, propMap);
            }

            return propMap;
        }

        public PropertyInfo[] GetProperties(Type type)
        {
            return GetPropertyMap(type).Values.ToArray();
        }

        public PropertyInfo GetPropertyForJsonKey(Type type, string jsonKey)
        {
            PropertyInfo propInfo;
            if (GetPropertyMap(type).TryGetValue(jsonKey, out propInfo)) return propInfo;
            else return null; // Or, throw an exception here??
        }

        #endregion

        public string GetJsonKeyForType(Type type)
        {
            string key = null;

            var keyCache = _jsonKeysForType.Value;

            lock (keyCache)
            {
                if (keyCache.TryGetValue(type, out key)) return key;

                if (IsSerializedAsMany(type))
                    type = GetElementType(type);

                var attrs = type.CustomAttributes.Where(x => x.AttributeType == typeof(Newtonsoft.Json.JsonObjectAttribute)).ToList();

                string title = type.Name;
                if (attrs.Any())
                {
                    var titles = attrs.First().NamedArguments.Where(arg => arg.MemberName == "Title")
                        .Select(arg => arg.TypedValue.Value.ToString()).ToList();
                    if (titles.Any()) title = titles.First();
                }

                key = FormatPropertyName(PluralizationService.Pluralize(title));

                keyCache.Add(type, key);
            }

            return key;
        }

        public string GetJsonKeyForProperty(PropertyInfo propInfo)
        {
            return FormatPropertyName(propInfo.Name);
            //TODO: Respect [JsonProperty(PropertyName = "FooBar")], and probably cache the result.
        }

        protected static string FormatPropertyName(string propertyName)
        {
            string result = propertyName.Substring(0, 1).ToLower() + propertyName.Substring(1);
            return result;
        }

        public bool IsSerializedAsMany(Type type)
        {
            bool isMany = 
                type.IsArray ||
                (type.GetInterfaces().Contains(typeof(System.Collections.IEnumerable)) && type.IsGenericType);

            return isMany;
        }

        public Type GetElementType(Type manyType)
        {
            Type etype = null;
            if (manyType.IsGenericType)
                etype = manyType.GetGenericArguments()[0];
            else
                etype = manyType.GetElementType();

            return etype;
        }

    }
}