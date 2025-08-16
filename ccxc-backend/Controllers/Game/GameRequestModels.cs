using Ccxc.Core.Utils.ExtensionFunctions;
using ccxc_backend.DataModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Controllers.Game
{
    public class PuzzleStartResponse : BasicResponse
    {
        public string start_prefix { get; set; }
        public int is_first { get; set; }
    }

    public class PuzzleArticleRequest
    {
        public string key { get; set; }
    }

    public class PuzzleGroupRequest
    {
        public int pgid { get; set; }
    }

    public class PuzzleArticleResponse : BasicResponse
    {
        public string title { get; set; }
        public string content { get; set; }
        public object data { get; set; }
    }

    public class NavbarItem
    {
        public string title { get; set; }
        public string path { get; set; }
    }
    public class PuzzleBasicInfo
    {
        public int pgid { get; set; }
        public string title { get; set; }
        public int is_finished { get; set; }
        public int is_unlocked { get; set; }
        public int difficulty { get; set; }
        public int stage { get; set; }
        public string content { get; set; }
    }
    public class DetailBasicInfo
    {
        public int pid { get; set; }
        public string title { get; set; }
        public int is_finished { get; set; }
        public int puzzle_type { get; set; }
        public List<string> icon { get; set; }
        public string answer { get; set; }
        public int is_unlocked { get; set; }
        public string extend_data { get; set; }
    }

    public class GetMainInfoResponse : BasicResponse
    {
        public string powerpoint_name { get; set; }
        public int stage { get; set; }
        public List<NavbarItem> nav_items { get; set; }
        public List<NavbarItem> nav_items2 { get; set; }
        public List<PuzzleBasicInfo> puzzle_basic_info { get; set; }
        public List<string> library_ids { get; set; }
        public int show_analysis { get; set; }
    }

    public class GetPuzzleInfoResponse : BasicResponse
    {
        public string title { get; set; }
        public string content { get; set; }
        public List<DetailBasicInfo> detail_basic_info { get; set; }
    }  

    public class GetDetailRequest
    {
        public int pid { get; set; }
    }

    public class GetPuzzleDetailResponse : BasicResponse
    {
        public PuzzleView puzzle { get; set; }
        public int is_finish { get; set; }
        public int attempts_count { get; set; }
        public int attempts_total { get; set; }

        public int power_point { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime power_point_calc_time  { get; set; }
        public int power_point_increase_rate { get; set; }
    }

    public class PuzzleView
    {
        public PuzzleView()
        {
            
        }
        public PuzzleView(puzzle p)
        {
            pid = p.pid;
            pgid = p.pgid;
            type = p.type;
            title = p.title;
            content = p.content;
            image = p.image;
            html = p.html;
            script = p.script;
            answer_type = p.answer_type;
        }
        public int pid { get; set; }
        public int pgid { get; set; }
        public string pg_name { get; set; }
        public int type { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string image { get; set; }
        public string html { get; set; }
        public string script { get; set; }
        public int answer_type { get; set; }
        public string extend_content { get; set; }
        public int adda { get; set; }
    }

    public class GetPuzzleListRequest
    {
        public int pgid { get; set; }
    }

    public class GetPuzzleDetailRequest
    {
        public int pid { get; set; }
    }

    public class GetPuzzleTipsResponse : BasicResponse
    {
        public int is_tip_available { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime tip_available_time { get; set; }
        public double tip_available_progress { get; set; }
        public int oracle_unlock_delay { get; set; }
        public int oracle_unlock_cost { get; set; }
        public int add_attempts_count_cost { get; set; }
        public List<PuzzleTipItem> puzzle_tips { get; set; }
        public List<OracleSimpleItem> oracles { get; set; }
    }

    public class UnlockPuzzleTipRequest
    {
        public int pid { get; set; }
        public int tip_num { get; set; }
    }

    public class PuzzleTipItem
    {
        public int tips_id { get; set; }

        /// <summary>
        /// 1/2/3
        /// </summary>
        public int tip_num { get; set; }

        public string title { get; set; }

        /// <summary>
        /// 0-未解锁 1-已解锁
        /// </summary>
        public int is_open { get; set; }
        public string content { get; set; }

        public int is_avaliable { get; set; }        
        
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime tip_available_time { get; set; }
        public double tip_available_progress { get; set; }

        public int unlock_cost { get; set; }
    }

    public class OracleSimpleItem
    {
        public int oracle_id { get; set; }
        public int is_reply { get; set; }
        
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime unlock_time { get; set; }
    }

    public class OpenOracleRequest
    {
        public int oracle_id { get; set; }
    }

    public class OpenOracleResponse : BasicResponse
    {
        public oracle data { get; set; }
    }

    public class EditOracleRequest
    {
        public int oracle_id { get; set; }
        public string question_content { get; set; }
    }
}
