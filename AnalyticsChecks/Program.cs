using System.Text.Json;
using CodexUsageMonitor;
var folder = Path.Combine(Path.GetTempPath(), "codex-usage-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(folder, "sessions"));
Environment.SetEnvironmentVariable("CODEX_HOME", folder);
string time = DateTimeOffset.Now.ToString("O");
string Event(string type, object payload) => JsonSerializer.Serialize(new { timestamp=time, type, payload });
string Count(long total, long last) => Event("event_msg", new {type="token_count", info=new {total_token_usage=new {total_tokens=total},last_token_usage=new {total_tokens=last}}});
string path = Path.Combine(folder, "sessions", "sample.jsonl");
File.WriteAllLines(path, new[] {
 Event("session_meta", new {cwd="C:/Project-A"}),
 Event("turn_context", new {model="model-a"}),
 Count(1000,100), Count(1000,100), Count(1250,250),
 Event("turn_context", new {model="model-b"}), Count(1400,150),
 Count(10,10), Count(40,30),
 "{partial"
});
var usage = new LocalUsage();
usage.Refresh();
var ranks = usage.Rank("7d", true);
if (ranks.Single(r=>r.Name=="model-a").Tokens != 350) throw new Exception("First baseline / duplicate counter failure");
if (ranks.Single(r=>r.Name=="model-b").Tokens != 180) throw new Exception("Model transition / reset failure");
if (usage.Rank("7d",false).Single().Tokens != 530) throw new Exception("Project aggregation failure");
usage.Refresh();
if (usage.Rank("7d",false).Single().Tokens != 530) throw new Exception("Refresh double counted");
Directory.CreateDirectory(Path.Combine(folder, "archived_sessions"));
string old = DateTimeOffset.Now.AddDays(-40).ToString("O");
File.WriteAllLines(Path.Combine(folder, "archived_sessions", "old.jsonl"),new[]{
 Event("session_meta",new {cwd="C:/Old"}),
 Event("turn_context",new {model="old-model"}),
 Count(80,80).Replace(time,old)
});
usage.Refresh();
if(usage.Rank("7d",true).Any(r=>r.Name=="old-model")) throw new Exception("Range filter failure");
if(!usage.Rank("all",true).Any(r=>r.Name=="old-model" && r.Tokens==80)) throw new Exception("Archive / all-range failure");
Console.WriteLine("PASS: deltas, duplicate counters, model switch, counter reset, partial line, repeat refresh, archive, date ranges.");
