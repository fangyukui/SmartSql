﻿using System;
using System.Collections;
using System.Linq;
using SmartSql.Data;
using SmartSql.Exceptions;
using SmartSql.Reflection;

namespace SmartSql.Configuration.Tags
{
    public class For : Tag
    {
        public const string FOR_KEY_SUFFIX = "_For_";
        public string Open { get; set; }
        public string Separator { get; set; }
        public string Close { get; set; }
        public string Key { get; set; }

        public override bool IsCondition(RequestContext context)
        {
            var reqVal = EnsurePropertyValue(context);
            if (reqVal == null) { return false; }
            if (reqVal is IEnumerable)
            {
                return (reqVal as IEnumerable).GetEnumerator().MoveNext();
            }
            return false;
        }
        public override void BuildChildSql(RequestContext context)
        {
            context.SqlBuilder.Append(Open);
            var reqVal = (EnsurePropertyValue(context) as IEnumerable).GetEnumerator();
            reqVal.MoveNext();
            bool isDirectValue = IsDirectValue(reqVal.Current);
            if (string.IsNullOrEmpty(Key))
            {
                throw new SmartSqlException("[For] tag [Key] is required!");
            }
            if (ChildTags.Count == 0)
            {
                throw new SmartSqlException("[For] tag must have childTag!");
            }
            if (!(ChildTags[0] is SqlText childText))
            {
                throw new SmartSqlException("[For] ChildTag only support SqlText!");
            }
            var itemSqlStr = childText.BodyText;
            if (isDirectValue)
            {
                BuildItemSql_DirectValue(itemSqlStr, context);
            }
            else
            {
                BuildItemSql_NotDirectValue(itemSqlStr, context);
            }
            context.SqlBuilder.Append(Close);
        }
        private void BuildItemSql_DirectValue(string itemSqlStr, RequestContext context)
        {
            var reqVal = (EnsurePropertyValue(context) as IEnumerable);
            int item_index = 0;
            string dbPrefix = GetDbProviderPrefix(context);
            foreach (var itemVal in reqVal)
            {
                if (item_index > 0)
                {
                    context.SqlBuilder.AppendFormat(" {0} ", Separator);
                }
                var item_sql = context.ExecutionContext.SmartSqlConfig.SqlParamAnalyzer
                    .Replace(itemSqlStr, (paramName, nameWithPrefix) =>
                {
                    string key_name = $"{Key}{FOR_KEY_SUFFIX}_{Property}_{item_index}";
                    context.Parameters.TryAdd(key_name, itemVal);
                    return $"{dbPrefix}{key_name}";
                });
                context.SqlBuilder.AppendFormat("{0}", item_sql);
                item_index++;
            }
        }
        private void BuildItemSql_NotDirectValue(string itemSqlStr, RequestContext context)
        {
            var reqVal = (EnsurePropertyValue(context) as IEnumerable);
            int item_index = 0;
            string dbPrefix = GetDbProviderPrefix(context);
            foreach (var itemVal in reqVal)
            {
                if (item_index > 0)
                {
                    context.SqlBuilder.AppendFormat(" {0} ", Separator);
                }
                var itemParams = RequestConvert.Instance.ToSqlParameters(itemVal, context.ExecutionContext.SmartSqlConfig.Settings.IgnoreParameterCase);

                var item_sql = context.ExecutionContext.SmartSqlConfig.SqlParamAnalyzer
                    .Replace(itemSqlStr, (paramName, nameWithPrefix) =>
                {
                    Parameter paramMap = null;
                    context.ParameterMap?.Parameters?.TryGetValue(paramName, out paramMap);
                    var propertyName = paramMap != null ? paramMap.Property : paramName;
                    string key_name = $"{Key}{FOR_KEY_SUFFIX}_{Property}_{paramName}_{item_index}";
                    if (!itemParams.TryGetValue(propertyName, out SqlParameter propertyVal))
                    {
                        return nameWithPrefix;
                    }
                    if (!context.Parameters.ContainsKey(key_name))
                    {
                        var itemSqlParameter = new SqlParameter(key_name, propertyVal.Value, propertyVal.ParameterType)
                        {
                            TypeHandler = paramMap?.Handler ?? propertyVal.TypeHandler
                        };
                        context.Parameters.Add(itemSqlParameter);
                    }
                    return $"{dbPrefix}{key_name}";
                });
                item_index++;
                context.SqlBuilder.AppendFormat("{0}", item_sql);
            }
        }

        private bool IsDirectValue(object obj)
        {
            bool isString = obj is String;
            if (isString) { return true; }
            bool isValueType = obj is ValueType;
            if (isValueType) { return true; }
            return false;
        }
    }
}
