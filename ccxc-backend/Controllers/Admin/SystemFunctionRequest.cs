using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Controllers.Admin
{
    public class PurgeCacheRequest
    {
        [Required(Message = "操作Key不能为空")]
        public string op_key { get; set; }
    }

    public class PrometheusResponse
    {
        public string status { get; set; }
        public PrometheusData data { get; set; }
    }

    public class PrometheusData
    {
        public string resultType { get; set; }
        public List<PrometheusResult> result { get; set; }
    }

    public class PrometheusResult
    {
        public Dictionary<string, string> metric { get; set; }
        public List<List<object>> values { get; set; }
    }

    public class PerformanceResponse : BasicResponse
    {
        public List<TimepointData> cpu { get; set; }
        public List<TimepointData> memory { get; set; }
        public List<TimepointData> disk_space { get; set; }
        public List<TimepointData> network_receive { get; set; }
        public List<TimepointData> network_send { get; set; }
    }

    public class TimepointData
    {
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime ts { get; set; } // 时间戳
        public double value { get; set; } // 数值
    }
}
