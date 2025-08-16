﻿using Ccxc.Core.HttpServer;
using ccxc_backend.DataServices;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class ImageUploaderController : HttpController
    {
        [HttpHandler("POST", "/admin/upload-prepare")]
        public async Task UploadPrepare(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member);
            if (userSession == null) return;

            var requestJson = request.Json<ImageUploadRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var cache = DbFactory.GetCache();
            var imageUploadToken = Guid.NewGuid().ToString("n");

            var imageUploadCacheKey = cache.GetDataKey($"upload_prepare_{imageUploadToken}");
            await cache.Put(imageUploadCacheKey, new ImagePrepareData
            {
                type = requestJson.type,
                token = imageUploadToken
            }, 300000);

            await response.JsonResponse(200, new ImagePrepareResponse
            {
                status = 1,
                message = "成功获取上传Token，请在300秒完成上传",
                upload_token = imageUploadToken
            });

        }

        [HttpHandler("POST", "/admin/upload-image")]
        public async Task UploadImage(Request request, Response response)
        {
            IDictionary<string, object> headers = request.Header;

            if (!headers.ContainsKey("upload-token"))
            {
                await response.Unauthorized("请求格式不完整：Upload-Token 不可为空。");
                return;
            }

            var uploadToken = headers["upload-token"].ToString();
            var cache = DbFactory.GetCache();
            var imageUploadCacheKey = cache.GetDataKey($"upload_prepare_{uploadToken}");
            var uploadPrepareObject = await cache.Get<ImagePrepareData>(imageUploadCacheKey);

            if(uploadPrepareObject == null)
            {
                await response.Unauthorized("未知的上传权限。");
                return;
            }


            if (request.RawRequest.Form.Files.Count == 1)
            {
                var fileDir = Config.Config.Options.ImageStorage;
                var fileGuid = Guid.NewGuid().ToString("n");

                var file = request.RawRequest.Form.Files[0];
                var fileExt = Path.GetExtension(file.FileName);

                if (uploadPrepareObject.type == 1)
                {
                    fileExt = ".webp";
                }

                var fileName = fileGuid + fileExt;
                var filePath = Path.Combine(fileDir, fileName);


                if (uploadPrepareObject.type == 1)
                {
                    //处理图片
                    using var memStream = new MemoryStream();
                    await file.CopyToAsync(memStream);
                    memStream.Position = 0; //准备 重用流

                    using var bitmap = SKBitmap.Decode(memStream);
                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    bitmap.Encode(fileStream, SKEncodedImageFormat.Webp, 100);
                }
                else
                {
                    //保存原文件
                    await using var fileStream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(fileStream);
                }
                

                await response.JsonResponse(200, new ImageResponse
                {
                    status = 1,
                    image_path = Config.Config.Options.ImagePrefix + fileName
                });
                return;
            }

            await response.BadRequest("未正确解析文件");
        }

        public class ImagePrepareData
        {
            public int type { get; set; }
            public string token { get; set; }
        }
    }
}
