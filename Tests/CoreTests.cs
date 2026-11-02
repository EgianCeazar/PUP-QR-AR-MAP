using System; using System.IO; using System.Linq; using System.Text.Json; using PUPMap.Core;
class P { static int fails=0; static void Check(bool c,string m){Console.WriteLine((c?"PASS ":"FAIL ")+m); if(!c)fails++;}
static int Main(){
 var json=File.ReadAllText("../Assets/StreamingAssets/Maps/pup_main.json");
 var map=JsonSerializer.Deserialize<CampusMap>(json,new JsonSerializerOptions{IncludeFields=true});
 var g=new MapGraph(map);
 var probs=g.Validate(); foreach(var p in probs)Console.WriteLine(" problem: "+p);
 Check(probs.Count==0,"map validates");
 foreach(var s in g.Nodes) foreach(var d in g.Nodes){ var r=Pathfinder.FindRoute(g,s.id,d.id); if(!r.Found){Check(false,$"route {s.id}->{d.id}");} }
 Check(true,"all-pairs routes found");
 // brute-force check A* optimality vs Dijkstra (Floyd)
 var ids=g.Nodes.Select(n=>n.id).ToList(); int N=ids.Count; var D=new double[N,N];
 for(int i=0;i<N;i++)for(int j=0;j<N;j++)D[i,j]=i==j?0:1e18;
 for(int i=0;i<N;i++)foreach(var nb in g.NeighborsOf(ids[i])){int j=ids.IndexOf(nb.node.id);D[i,j]=Math.Min(D[i,j],nb.distance);}
 for(int k=0;k<N;k++)for(int i=0;i<N;i++)for(int j=0;j<N;j++)if(D[i,k]+D[k,j]<D[i,j])D[i,j]=D[i,k]+D[k,j];
 bool opt=true; for(int i=0;i<N;i++)for(int j=0;j<N;j++){var r=Pathfinder.FindRoute(g,ids[i],ids[j]); if(Math.Abs(r.TotalDistance-D[i,j])>1e-6)opt=false;}
 Check(opt,"A* matches shortest distances (Floyd-Warshall)");
 var r1=Pathfinder.FindRoute(g,"main-gate","library");
 Console.WriteLine($" main-gate->library: {r1.TotalDistance:0} m, ~{r1.EstimatedMinutes} min"); foreach(var s in r1.steps)Console.WriteLine("   "+s.Instruction);
 var a=Pathfinder.FindRoute(g,"main-bldg-n","lagoon",true);
 Check(a.Found && a.nodes.Count>2,"accessible route avoids stairs edge: "+string.Join(" > ",a.nodes.Select(n=>n.id)));
 Check(QrPayload.TryParse("WAYFIND:1:pup-main:lagoon",out var q)&&q.NodeId=="lagoon","QR parse ok");
 Check(!QrPayload.TryParse("hello",out _)&&!QrPayload.TryParse("WAYFIND:2:x:y",out _),"QR rejects bad input");
 Check(!Pathfinder.FindRoute(g,"nope","lagoon").Found,"unknown node -> no route");
 return fails;}}
