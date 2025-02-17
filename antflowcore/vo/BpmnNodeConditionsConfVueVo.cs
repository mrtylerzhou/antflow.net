﻿
using System;
using System.Text.Json.Serialization;

namespace Antflowcore.Vo
{
    public class BpmnNodeConditionsConfVueVo
    {
        [JsonPropertyName("showType")]
        public string ShowType { get; set; }

        [JsonPropertyName("columnId")]
        public string ColumnId { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("showName")]
        public string ShowName { get; set; }

        [JsonPropertyName("optType")]
        public int? OptType { get; set; }

        [JsonPropertyName("zdy1")]
        public string Zdy1 { get; set; }

        [JsonPropertyName("opt1")]
        public string Opt1 { get; set; }

        [JsonPropertyName("zdy2")]
        public string Zdy2 { get; set; }

        [JsonPropertyName("opt2")]
        public string Opt2 { get; set; }

        [JsonPropertyName("columnDbname")]
        public string ColumnDbname { get; set; }

        [JsonPropertyName("columnType")]
        public string ColumnType { get; set; }

        [JsonPropertyName("fixedDownBoxValue")]
        public string FixedDownBoxValue { get; set; }
    }
}