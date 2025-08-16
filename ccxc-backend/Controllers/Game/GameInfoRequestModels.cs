using System;
using System.Collections.Generic;
using System.Text;
using ccxc_backend.DataModels;
using Newtonsoft.Json;

namespace ccxc_backend.Controllers.Game
{
    public class GetLastAnswerLogRequest
    {
        public int pid { get; set; }
    }

    public class GetLastAnswerLogResponse : BasicResponse
    {
        public List<AnswerLogView> answer_log { get; set; }
    }

    public class AnswerLogView : answer_log
    {
        public AnswerLogView(answer_log a)
        {
            id = a.id;
            create_time = a.create_time;
            uid = a.uid;
            gid = a.gid;
            pid = a.pid;
            answer = a.answer;
            status = a.status;
            message = a.message;
        }

        public string user_name { get; set; }
    }

    public class PuzzleStat
    {
        public int pid { get; set; }
        public int pgid { get; set; }
        public string title { get; set; }
        public int unlock_count { get; set; }
        public int finish_count { get; set; }

        public string last_answer_person_name { get; set; }
    }

    public class PuzzleStatCache
    {
        public List<PuzzleStat> data { get; set; }

        [JsonConverter(typeof(Ccxc.Core.Utils.ExtensionFunctions.UnixTimestampConverter))]
        public DateTime cache_time { get; set; }
    }

    public class GetPuzzleBoardResponse : BasicResponse
    {
        public List<PuzzleStat> data { get; set; }
        [JsonConverter(typeof(Ccxc.Core.Utils.ExtensionFunctions.UnixTimestampConverter))]
        public DateTime cache_time { get; set; }
    }

    public class StoryItem
    {
        public string key { get; set; }
        public string title { get; set; }
    }

    public class GetLibraryResponse : BasicResponse
    {
        public List<StoryItem> data { get; set; }
    }

    public class GetPuzzleAnalysisResponse : BasicResponse
    {
        public string title { get; set; }
        public string author { get; set; }
        public string analysis { get; set; }
        public string answer { get; set; }
        public int check_answer_type { get; set; }
    }
}
