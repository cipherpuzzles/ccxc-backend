using Ccxc.Core.HttpServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    public class PuzzleArticleRequest
    {
        public string key { get; set; }
        [Required(Message = "标题不能为空")]
        public string title { get; set; }
        [Required(Message = "内容不能为空")]
        public string content { get; set; }
        public int is_hide { get; set; }
    }

    public class DeletePuzzleArticleRequest
    {
        public int paid { get; set; }
    }

    public class EditPuzzleArticleRequest
    {
        public int paid { get; set; }
        public string key { get; set; }
        [Required(Message = "标题不能为空")]
        public string title { get; set; }
        [Required(Message = "内容不能为空")]
        public string content { get; set; }
        public int is_hide { get; set; }
    }

    public class ListPuzzleArticleResponse : BasicResponse
    {
        public List<DataModels.puzzle_article> puzzle_articles { get; set; }
    }
}
