using Ccxc.Core.HttpServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Announcements
{
    public class ArticleRequest
    {
        public int order { get; set; }
        [Required(Message = "标题不能为空")]
        public string title { get; set; }
        public string path { get; set; }
        [Required(Message = "内容不能为空")]
        public string content { get; set; }
        public int is_hide { get; set; }
    }

    public class DeleteArticleRequest
    {
        public int aid { get; set; }
    }

    public class EditArticleRequest
    {
        public int aid { get; set; }
        public int order { get; set; }
        [Required(Message = "标题不能为空")]
        public string title { get; set; }
        public string path { get; set; }
        [Required(Message = "内容不能为空")]
        public string content { get; set; }
        public int is_hide { get; set; }
    }

    public class GetArticleRequest
    {
        [Required(Message = "请求文章不能为空")]
        public string path { get; set; }
    }

    public class GetArticleResponse : BasicResponse
    {
        public DataModels.article article { get; set; }
        public List<ArticleSimpleView> list { get; set; }
    }

    public class ArticleSimpleView
    {
        public string title { get; set; }
        public string path { get; set; }
    }

    public class ListArticleResponse : BasicResponse
    {
        public List<DataModels.article> articles { get; set; }
    }
}
