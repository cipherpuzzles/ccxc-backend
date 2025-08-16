using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    public class PuzzleScriptRequest
    {
        public string key { get; set; }
        [Required(Message = "标题不能为空")]
        public string desc { get; set; }
        [Required(Message = "内容不能为空")]
        public string script { get; set; }
    }

    public class DeletePuzzleScriptRequest
    {
        public int psid { get; set; }
    }

    public class EditPuzzleScriptRequest
    {
        public int psid { get; set; }
        public string key { get; set; }
        [Required(Message = "标题不能为空")]
        public string desc { get; set; }
        [Required(Message = "内容不能为空")]
        public string script { get; set; }
    }

    public class ListPuzzleScriptResponse : BasicResponse
    {
        public List<puzzle_backend_script> puzzle_scripts { get; set; }
    }

    public class AIScriptCompletionRequest
    {
        public AIScriptCompletionMetaData completionMetadata { get; set; }
    }

    public class AIScriptCompletionMetaData
    {
        public string language { get; set; }
        public string textAfterCursor { get; set; }
        public string textBeforeCursor { get; set; }
    }

    public class AIScriptCompletionResponse : BasicResponse
    {
        public string completion { get; set; }
    }
}
