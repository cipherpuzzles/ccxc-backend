using Ccxc.Core.DbOrm;
using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils.ExtensionFunctions;
using ccxc_backend.DataModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace ccxc_backend.Controllers.Announcements
{
    public class AddAnnoRequest
    {
        [Required(Message = "内容不能为空")]
        public string content { get; set; }
        public int is_hide { get; set; }
    }

    public class DeleteAnnoRequest
    {
        public int aid { get; set; }
    }

    public class EditAnnoRequest
    {
        public int aid { get; set; }

        [Required(Message = "内容不能为空")]
        public string content { get; set; }
        public int is_hide { get; set; }
    }

    public class GetAnnoRequest
    {
        public List<int> aids { get; set; }
    }

    public class AnnoItem
    {
        public int aid { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime update_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime create_time { get; set; }

        public string content { get; set; }
        public int type { get; set; }

        public AnnoItem(announcement anno)
        {
            aid = anno.aid;
            update_time = anno.update_time;
            create_time = anno.create_time;
            content = anno.content;
            type = anno.is_hide;
        }

        public AnnoItem() { }
    }

    public class GetAnnoResponse : BasicResponse
    {
        public List<AnnoItem> announcements { get; set; }
    }

    public class GetTempAnnoResponse : BasicResponse
    {
        public List<temp_anno> temp_anno { get; set; }
    }

    public class ConvertTempAnnoRequest
    {
        public int pid { get; set; }
    }

    public class AddTextRequest
    {
        public TextMemory data { get; set; }
    }

    public class AddTextResponse : BasicResponse
    {
        public string tid { get; set; }
    }

    public class GetTextRequest
    {
        public string tid { get; set; }
        public string code { get; set; }
    }

    public class GetTextResponse : BasicResponse
    {
        public TextMemory data { get; set; }
    }

    public class TextMemory
    {
        public string title { get; set; }
        public string content { get; set; }
        public string extract_code { get; set; }
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime expiration_time { get; set; }
    }
}
