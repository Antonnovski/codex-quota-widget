using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

static class Program {
 [STAThread] static void Main(string[] args) {
  using var mutex=new Mutex(true,"Local\\CodexQuotaWidget_v1",out bool first); if(!first)return;
  var app=new Application(); app.Run(new Widget());
 }
}
class Widget:Window {
 static Brush B(string s)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));
 readonly WrapPanel cards=new(){Orientation=Orientation.Horizontal}; readonly TextBlock status=T("正在连接 Codex…",11,"#9AA9BF");
 readonly TextBlock plan=T("账户额度",12,"#B0A5C4");
 readonly Button refresh; readonly List<(TextBlock,long?)> timers=new();
 readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromSeconds(1)};
 DateTime last=DateTime.MinValue,attempt=DateTime.MinValue; bool busy,failed; Process? server;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexQuotaWidget","position.json");
 public Widget(){
  Title="Codex 额度小组件";Width=330;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;FontFamily=new FontFamily("Microsoft YaHei UI");
  var glass=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1),GradientStops=new GradientStopCollection{new GradientStop((Color)ColorConverter.ConvertFromString("#E0222440"),0),new GradientStop((Color)ColorConverter.ConvertFromString("#C7151A31"),.48),new GradientStop((Color)ColorConverter.ConvertFromString("#E00A1022"),1)}};
  var shell=new Border{CornerRadius=new CornerRadius(21),BorderBrush=B("#A6C4D0FF"),BorderThickness=new Thickness(1.2),Padding=new Thickness(23),Margin=new Thickness(10),Background=glass,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=(Color)ColorConverter.ConvertFromString("#5369A8"),BlurRadius=9,ShadowDepth=0,Opacity=.20}};
  shell.LayoutTransform=new ScaleTransform(.84,.84);Content=shell;var layers=new Grid();shell.Child=layers;
  var glow=new System.Windows.Shapes.Ellipse{Width=245,Height=170,Fill=new RadialGradientBrush((Color)ColorConverter.ConvertFromString("#706A9FFF"),(Color)ColorConverter.ConvertFromString("#006A9FFF")),Opacity=.42,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.BlurEffect{Radius=28},HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(-85,-80,0,0)};layers.Children.Add(glow);
  var sheen=new Border{Height=72,CornerRadius=new CornerRadius(20,20,55,55),Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#50FFFFFF"),(Color)ColorConverter.ConvertFromString("#00FFFFFF"),90),Opacity=.32,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};layers.Children.Add(sheen);
  var ornament=Rose(118,"#899FFF");ornament.Opacity=.18;ornament.HorizontalAlignment=HorizontalAlignment.Right;ornament.VerticalAlignment=VerticalAlignment.Top;ornament.Margin=new Thickness(0,45,-10,0);layers.Children.Add(ornament);var root=new StackPanel();layers.Children.Add(root);
  var head=new Grid{Margin=new Thickness(0,0,0,9)};head.ColumnDefinitions.Add(new());head.ColumnDefinitions.Add(new(){Width=GridLength.Auto});root.Children.Add(head);
  var brand=new StackPanel();brand.Children.Add(new TextBlock{Text="Roselia",FontFamily=new FontFamily("Georgia"),FontStyle=FontStyles.Italic,FontSize=34,Foreground=B("#E4D8FF"),Margin=new Thickness(0,-5,0,0)});brand.Children.Add(plan);head.Children.Add(brand);brand.Cursor=Cursors.SizeAll;brand.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed){DragMove();SavePosition();}};
  var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};Grid.SetColumn(actions,1);head.Children.Add(actions);
  var pin=Btn("置顶",44);pin.Foreground=B("#91BBFF");pin.ToolTip="切换始终置顶";pin.Click+=(_,_)=>{Topmost=!Topmost;pin.Content=Topmost?"置顶":"普通";pin.Foreground=B(Topmost?"#91BBFF":"#B0BED1");};actions.Children.Add(pin);
  var close=Btn("×",28);close.ToolTip="关闭小组件";close.Click+=(_,_)=>Close();actions.Children.Add(close);
  var flourish=new Grid{Margin=new Thickness(0,0,0,13),Height=23};flourish.Children.Add(new Border{Height=1,VerticalAlignment=VerticalAlignment.Center,Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#20FFFFFF"),(Color)ColorConverter.ConvertFromString("#B0AFC5FF"),0)});var seal=Rose(27,"#C8D3FF");seal.HorizontalAlignment=HorizontalAlignment.Center;flourish.Children.Add(seal);root.Children.Add(flourish);
  root.Children.Add(cards); cards.Children.Add(T("正在读取你的剩余额度",17,"#E3EBF7",true));
  var foot=new Grid{Margin=new Thickness(0,16,0,0)};foot.ColumnDefinitions.Add(new());foot.ColumnDefinitions.Add(new(){Width=GridLength.Auto});root.Children.Add(foot);
  var info=new StackPanel();info.Children.Add(status);info.Children.Add(T("每分钟同步 · 北京时间 UTC+8",10,"#A298B7"));foot.Children.Add(info);
  refresh=Btn("↻ 刷新",64);refresh.ToolTip="立即获取最新额度";refresh.Click+=async(_,_)=>await Refresh();Grid.SetColumn(refresh,1);foot.Children.Add(refresh);
  var wa=SystemParameters.WorkArea;Left=wa.Right-Width-24;Top=wa.Top+70;
  try{var p=JsonDocument.Parse(File.ReadAllText(settings)).RootElement;Left=Math.Clamp(p.GetProperty("left").GetDouble(),wa.Left,wa.Right-Width);Top=Math.Clamp(p.GetProperty("top").GetDouble(),wa.Top,wa.Bottom-450);}catch{}
  Loaded+=async(_,_)=>{clock.Start();await Refresh();};clock.Tick+=async(_,_)=>{Tick();if(!busy&&(DateTime.UtcNow-attempt).TotalSeconds>=60)await Refresh();};
  Closed+=(_,_)=>{clock.Stop();SavePosition();try{server?.Kill(true);}catch{}};
 }
 static FrameworkElement Rose(double size,string color){
  var canvas=new Canvas{Width=100,Height=100,IsHitTestVisible=false};
  var paths=new[]{"M50,9 C65,0 81,11 78,28 C98,26 104,48 87,60 C94,80 73,95 59,85 C48,104 26,96 24,79 C3,83 -4,61 12,49 C-2,33 10,15 29,20 C32,7 41,4 50,9 Z", "M49,20 C67,15 83,33 76,47 C90,65 66,84 53,74 C36,91 17,71 24,56 C9,41 29,23 42,30 Z", "M42,30 C58,22 75,39 64,52 C75,67 48,80 39,64 C22,65 22,43 37,42 Z", "M37,42 C41,32 60,32 62,44 C60,53 45,49 44,60 C33,54 33,47 37,42 Z", "M44,60 C59,65 71,55 76,47 M24,56 C31,48 34,45 37,42 M53,74 C51,84 56,87 59,85 M29,20 C24,29 23,35 25,39"};
  foreach(var d in paths)canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(d),Stroke=B(color),StrokeThickness=1.7,StrokeLineJoin=PenLineJoin.Round,Fill=Brushes.Transparent});
  return new Viewbox{Width=size,Height=size,Child=canvas,IsHitTestVisible=false};
 }
 void SavePosition(){try{Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,JsonSerializer.Serialize(new{left=Left,top=Top}));}catch{}}
 static TextBlock T(string text,double size,string color,bool bold=false)=>new(){Text=text,FontSize=size,Foreground=B(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Margin=new Thickness(0,2,0,2)};
 static Button Btn(string text,int width){var b=new Button{Content=text,Width=width,Height=29,Margin=new Thickness(3,0,0,0),Background=B("#594F527C"),Foreground=B("#E5EBFF"),BorderBrush=B("#70C8D3FF"),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,FontSize=11,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=9,ShadowDepth=2,Opacity=.22}};var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));var cp=new FrameworkElementFactory(typeof(ContentPresenter));cp.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);cp.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(cp);b.Template=new ControlTemplate(typeof(Button)){VisualTree=border};return b;}
 static JsonElement Prop(JsonElement e,string k)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(k,out var v)?v:default;
 static double? Num(JsonElement e,string k){var v=Prop(e,k);return v.ValueKind==JsonValueKind.Number?v.GetDouble():null;}
 static string? Str(JsonElement e,string k){var v=Prop(e,k);return v.ValueKind==JsonValueKind.String?v.GetString():null;}
 public static double? Remaining(double? used)=>used.HasValue?Math.Clamp(100-used.Value,0,100):null;
 static string Duration(double? m)=>m==10080?"每周额度":m==300?"5 小时额度":m.HasValue?(m>=1440?$"{m/1440:0.#} 天额度":m>=60?$"{m/60:0.#} 小时额度":$"{m:0} 分钟额度"):"额度窗口";
 void AddCard(JsonElement window,string fallback,string accent){
  var remaining=Remaining(Num(window,"usedPercent"));long? reset=Num(window,"resetsAt") is double ts?(long)ts:null;
  var cardGlass=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1),GradientStops=new GradientStopCollection{new GradientStop((Color)ColorConverter.ConvertFromString("#704D5276"),0),new GradientStop((Color)ColorConverter.ConvertFromString("#48232A4C"),.58),new GradientStop((Color)ColorConverter.ConvertFromString("#70323A66"),1)}};
  var panel=new StackPanel();var box=new Border{Width=156,CornerRadius=new CornerRadius(15),BorderBrush=B("#78C5D2FF"),BorderThickness=new Thickness(1),Background=cardGlass,Padding=new Thickness(11,11,11,12),Margin=new Thickness(cards.Children.Count==0?0:8,0,0,0),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=(Color)ColorConverter.ConvertFromString("#090C20"),BlurRadius=14,ShadowDepth=3,Opacity=.34}};var cardLayers=new Grid();var bloom=Rose(62,accent);bloom.Opacity=.19;bloom.HorizontalAlignment=HorizontalAlignment.Right;bloom.VerticalAlignment=VerticalAlignment.Top;bloom.Margin=new Thickness(0,4,0,0);cardLayers.Children.Add(bloom);var shine=new Border{Height=28,CornerRadius=new CornerRadius(14,14,28,28),Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#42FFFFFF"),(Color)ColorConverter.ConvertFromString("#00FFFFFF"),90),Opacity=.38,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};cardLayers.Children.Add(shine);cardLayers.Children.Add(panel);box.Child=cardLayers;cards.Children.Add(box);
  panel.Children.Add(T(window.ValueKind==JsonValueKind.Object?Duration(Num(window,"windowDurationMins")):fallback,12,"#BFCCE0",true));
  var row=new StackPanel{Orientation=Orientation.Horizontal};panel.Children.Add(row);var color=remaining<=15?"#FFB68C":accent;
  var number=T(remaining.HasValue?$"{remaining:0.#}%":"—",38,color,true);number.FontFamily=new FontFamily("Georgia");row.Children.Add(number);row.Children.Add(new TextBlock{Text="剩余",Foreground=B("#ADBBD0"),FontSize=10,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(5,0,0,10)});
  var track=new Border{Height=6,CornerRadius=new CornerRadius(3),Background=B("#50333758"),BorderBrush=B("#35FFFFFF"),BorderThickness=new Thickness(.5),Margin=new Thickness(0,4,0,10)};var fill=new Border{Height=5,CornerRadius=new CornerRadius(2.5),Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#8294FF"),(Color)ColorConverter.ConvertFromString(color),0),HorizontalAlignment=HorizontalAlignment.Left,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=(Color)ColorConverter.ConvertFromString(color),BlurRadius=7,ShadowDepth=0,Opacity=.7}};track.Child=fill;track.SizeChanged+=(_,_)=>fill.Width=track.ActualWidth*(remaining??0)/100;panel.Children.Add(track);
  string date="重置时间暂不可用";if(reset.HasValue){try{date="重置于 "+DateTimeOffset.FromUnixTimeSeconds(reset.Value).ToOffset(TimeSpan.FromHours(8)).ToString("MM月dd日  HH:mm");}catch{reset=null;}}
  panel.Children.Add(T(date,10,"#E0E8F5"));var countdown=T("",10,"#96A9C4");panel.Children.Add(countdown);timers.Add((countdown,reset));
 }
 void Render(JsonElement result){
  cards.Children.Clear();timers.Clear();var map=Prop(result,"rateLimitsByLimitId");var buckets=new List<JsonElement>();
  if(map.ValueKind==JsonValueKind.Object){foreach(var p in map.EnumerateObject())buckets.Add(p.Value);} if(buckets.Count==0)buckets.Add(Prop(result,"rateLimits"));
  plan.Text="CODEX  /  账户额度 · "+(Str(buckets[0],"planType")??"Codex").ToUpperInvariant();
  foreach(var b in buckets){if(buckets.Count>1)cards.Children.Add(T(Str(b,"limitName")??Str(b,"limitId")??"Codex",12,"#C0CDE1",true));AddCard(Prop(b,"primary"),"短时额度","#91BBFF");AddCard(Prop(b,"secondary"),"长期额度","#D0AAFF");}
  Tick();
 }
 void Tick(){foreach(var(t,ts)in timers){if(!ts.HasValue){t.Text="等待账户提供时间";continue;}var sec=ts.Value-DateTimeOffset.UtcNow.ToUnixTimeSeconds();if(sec<=0)t.Text="重置时间已到 · 等待同步确认";else{var d=TimeSpan.FromSeconds(sec);t.Text=d.Days>0?$"还有 {d.Days} 天 {d.Hours} 小时 {d.Minutes} 分钟":$"还有 {d.Hours:00}:{d.Minutes:00}:{d.Seconds:00}";}}
  if(!busy&&last!=DateTime.MinValue){var age=(int)(DateTime.UtcNow-last).TotalMinutes;status.Text=failed?$"● 同步失败 · {age} 分钟前的数据":$"● 已同步 {last.ToLocalTime():HH:mm:ss}";status.Foreground=B(failed?"#FFB68C":"#91BBFF");}
 }
 static string FindCodex(){var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");if(Directory.Exists(root)){var files=Directory.GetFiles(root,"codex.exe",SearchOption.AllDirectories);if(files.Length>0)return files.OrderByDescending(File.GetLastWriteTimeUtc).First();}foreach(var dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')){var p=Path.Combine(dir,"codex.exe");if(File.Exists(p))return p;}throw new Exception("请先安装 Codex 并登录");}
 async Task<JsonElement> ReadResponse(Process p,int id,CancellationToken ct){while(true){var line=await p.StandardOutput.ReadLineAsync(ct);if(line==null)throw new Exception("Codex 连接已断开");try{using var d=JsonDocument.Parse(line);var root=d.RootElement;if(Num(root,"id")!=id)continue;if(Prop(root,"error").ValueKind==JsonValueKind.Object)throw new InvalidOperationException("无法获取额度，请检查 Codex 登录状态");return Prop(root,"result").Clone();}catch(JsonException){}}}
 async Task Refresh(){if(busy)return;busy=true;attempt=DateTime.UtcNow;refresh.IsEnabled=false;status.Text="● 正在同步…";
  try{using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(25));var start=new ProcessStartInfo(FindCodex(),"app-server --stdio"){RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};using var p=Process.Start(start)??throw new Exception("无法启动额度连接");server=p;p.ErrorDataReceived+=(_,_)=>{};p.BeginErrorReadLine();
   try{await p.StandardInput.WriteLineAsync("{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"quota_desktop_widget\",\"version\":\"1.0.0\"}}}");await ReadResponse(p,1,ct.Token);await p.StandardInput.WriteLineAsync("{\"method\":\"initialized\"}");await p.StandardInput.WriteLineAsync("{\"id\":2,\"method\":\"account/rateLimits/read\"}");var data=await ReadResponse(p,2,ct.Token);last=DateTime.UtcNow;failed=false;Render(data);status.ToolTip="来自当前 Codex 登录账户";}
   finally{try{if(!p.HasExited)p.Kill(true);}catch{}server=null;}
  }catch(Exception ex){failed=true;status.Text=last==DateTime.MinValue?"● 连接失败 · 点击刷新重试":"● 同步失败 · 显示上次数据";status.Foreground=B("#FFB68C");status.ToolTip=ex is OperationCanceledException?"连接超时，请检查网络后重试":ex.Message;if(last==DateTime.MinValue){cards.Children.Clear();cards.Children.Add(new TextBlock{Text="暂时无法读取额度\n请确认 Codex 已登录，再点击刷新。",Foreground=B("#DAE4F3"),TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,15,0,20)});}}
  finally{busy=false;refresh.IsEnabled=true;Tick();}
 }
}
