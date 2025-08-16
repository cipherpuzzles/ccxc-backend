using System;
using System.Collections.Generic;
using System.Text;
using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils.ExtensionFunctions;
using ccxc_backend.DataModels;
using Newtonsoft.Json;

namespace ccxc_backend.Controllers.Admin
{
    public class AddPuzzleRequest
    {
        public int pid { get; set; }
        public int pgid { get; set; }
        public string desc { get; set; }
        public byte type { get; set; }

        [Required(Message = "标题不能为空")]
        public string title { get; set; }
        public string author { get; set; }

        public string content { get; set; }
        public string image { get; set; }
        public string html { get; set; }
        public string script { get; set; }
        public byte answer_type { get; set; }

        [Required(Message = "答案不能为空")]
        public string answer { get; set; }
        public int check_answer_type { get; set; }
        public string check_answer_function { get; set; }
        public int attempts_count { get; set; }
        public string jump_keyword { get; set; }
        public string extend_content { get; set; }
        public string extend_data { get; set; }
        public string analysis { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime last_dt_update { get; set; }
    }

    public class DeletePuzzleRequest
    {
        public int pid { get; set; }
    }

    public class GetPuzzleResponse : BasicResponse
    {
        public List<puzzle> puzzle { get; set; }
    }

    public class GetAdditionalAnswerResponse : BasicResponse
    {
        public List<additional_answer> additional_answer { get; set; }
    }

    public class DeleteAdditionalAnswerRequest
    {
        public int aaid { get; set; }
    }

    public class GetTipsResponse : BasicResponse
    {
        public List<puzzle_tips> tips { get; set; }
    }

    public class DeleteTipsRequest
    {
        public int ptid { get; set; }
    }

    public class SwapPidsRequest
    {
        public int pid1 { get; set; }
        public int pid2 { get; set; }
    }
}
