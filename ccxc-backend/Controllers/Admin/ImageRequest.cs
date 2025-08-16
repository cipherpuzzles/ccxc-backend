using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Controllers.Admin
{
    public class ImageUploadRequest
    {
        /// <summary>
        /// 0-保存原文件 1-处理图片转为jpg
        /// </summary>
        public int type { get; set; }
    }
    public class ImageResponse : BasicResponse
    {
        public string image_path { get; set; }
    }

    public class ImagePrepareResponse : BasicResponse
    {
        public string upload_token { get; set; }
    }
}
