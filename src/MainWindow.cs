using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LamAngStudio
{
    public sealed class MainWindow:Window
    {
        Profile profile=new Profile(),saved;
        Profile appliedActions;
        readonly HashSet<string> pending=new HashSet<string>{"lighting"};
        readonly DeviceState deviceState=new DeviceState();
        readonly HashSet<int> selection=new HashSet<int>();
        readonly List<Tuple<Button,Border,int>> keyButtons=new List<Tuple<Button,Border,int>>();
        readonly List<Grid> layouts=new List<Grid>();
        readonly HashSet<TextBox> invalid=new HashSet<TextBox>();
        readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};
        List<DeviceInfo> devices=new List<DeviceInfo>();
        ComboBox devicePicker,effectPicker,actionPicker,macroPicker;
        TabControl tabs;
        TextBlock connection,status,detail,pendingLabel,keyTitle,selectionLabel,brightnessLabel,speedLabel;
        Button applyButton,recordButton;
        StackPanel colorControls,directionControls,assignmentControls,customControls;
        CheckBox fullColor,lightingOn;
        Slider brightness,speed;
        TextBox hex,profileName,macroName,repeats,fixedDelay;
        ListBox profileList,eventList;
        string devicePath="",lastLog="",actionContainer="";
        bool busy,refreshing,suppress;
        int revision,selectedUsage=57;
        Color paint=Color.FromRgb(168,199,250);
        HostActions host;
        Macro recordingMacro;
        Assignment editing;
        ComboBox destination;
        TextBox target;
        List<CheckBox> modifiers=new List<CheckBox>();
        List<ComboBox> chord=new List<ComboBox>();
        static readonly string[] Kinds={"Default","Keyboard","Chord","Macro","Mouse","Media","Windows","Program","Website","Text","Profile","Disabled"};
        static readonly string[] KindNames={"Default key","Keyboard key","Combination (up to 3 keys)","Onboard macro","Mouse action","Multimedia","Windows shortcut","Open program","Open website","Send text","Switch local profile","Disabled"};
        static readonly string[] MouseNames={"Left click","Middle click","Right click","Back button","Forward button","Scroll up","Scroll down"};
        static readonly string[] MediaNames={"Play / pause","Stop","Previous track","Next track","Volume up","Volume down","Mute"};
        static readonly int[] WindowsIds={1,2,3,4,5,6,7,8,13,14,15,16};
        static readonly string[] WindowsNames={"Show desktop","File Explorer","Lock computer","Close tab / window (Ctrl+W)","Switch applications","Copy","Paste","Cut","Calculator","Browser","E-mail","Media player"};
        readonly string profilesFolder=Path.Combine(DeviceWorker.DataFolder,"Profiles");
        Brush B(string name){return (Brush)Application.Current.Resources[name];}
        public MainWindow()
        {
            Title="Lam-Ang Studio";Width=1220;Height=820;MinWidth=900;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI");FontSize=14;
            Directory.CreateDirectory(profilesFolder);
            string last=Path.Combine(DeviceWorker.DataFolder,"Draft.json");
            if(File.Exists(last)){try{profile=ProfileStore.Load(last);}catch(Exception){status=null;}}
            else{profile.Lights[0].FullColor=false;profile.Lights[0].Red=168;profile.Lights[0].Green=199;profile.Lights[0].Blue=250;profile.Lights[0].Brightness=8;}
            saved=ProfileStore.Clone(profile);ApplyAppearance();BuildUI();
            SourceInitialized+=delegate{try{host=new HostActions(this);host.DeviceAction=HostAction;host.Recorded=RecordEvent;host.RecordingStopped=RecordingStopped;}catch(Exception e){ShowStatus("Extended-action listener unavailable",e.Message,true);}};
            Loaded+=async delegate{await RefreshDevices();timer.Start();};
            timer.Tick+=async delegate{if(!busy)await RefreshDevices();};
            SizeChanged+=delegate{LayoutEditors();};
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;ShowStatus("Applying settings", "Please wait for this device operation to finish (15-second maximum).",false);return;}timer.Stop();if(host!=null)host.Dispose();try{ProfileStore.Save(last,profile);}catch(Exception){};};
            AddCommand(Key.Enter,ModifierKeys.Control,async delegate{await ApplyPending();});
            AddCommand(Key.S,ModifierKeys.Control,SaveLocal);
            AddCommand(Key.O,ModifierKeys.Control,ImportProfile);
            AddCommand(Key.F5,ModifierKeys.None,async delegate{await RefreshDevices();});
        }
        void AddCommand(Key key,ModifierKeys modifiers,Action action)
        {var command=new RoutedCommand();CommandBindings.Add(new CommandBinding(command,delegate{action();}));InputBindings.Add(new KeyBinding(command,new KeyGesture(key,modifiers)));}
        void ApplyAppearance()
        {
            bool light=false;try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))light=key!=null&&Convert.ToInt32(key.GetValue("AppsUseLightTheme",0))==1;}catch(Exception){}
            var colors=light?new[]{"#F5F6F8","#FFFFFF","#E9ECF1","#CDD3DC","#20242B","#5A6370","#245AA8","#FFFFFF","#885700"}:new[]{"#181A1D","#22252A","#2C3036","#3E444E","#F2F3F5","#ADB3BD","#A8C7FA","#181A1D","#F6C777"};
            string[] names={"Surface","Panel","Raised","Line","Text","Secondary","Accent","OnAccent","Attention"};
            for(int i=0;i<names.Length;i++)Application.Current.Resources[names[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
            if(SystemParameters.HighContrast){Application.Current.Resources["Surface"]=SystemColors.WindowBrush;Application.Current.Resources["Panel"]=SystemColors.ControlBrush;Application.Current.Resources["Raised"]=SystemColors.HighlightBrush;Application.Current.Resources["Text"]=SystemColors.WindowTextBrush;Application.Current.Resources["Secondary"]=SystemColors.WindowTextBrush;Application.Current.Resources["Accent"]=SystemColors.HighlightBrush;Application.Current.Resources["OnAccent"]=SystemColors.HighlightTextBrush;Application.Current.Resources["Line"]=SystemColors.WindowTextBrush;}
            Background=B("Surface");Foreground=B("Text");
        }
        TextBlock Text(string text,int size,bool secondary=false)
        {return new TextBlock{Text=text,FontSize=size,Foreground=B(secondary?"Secondary":"Text"),TextWrapping=TextWrapping.Wrap};}
        Button Button(string title,Action action,bool primary=false)
        {var button=new Button{Content=title,Margin=new Thickness(0,0,7,0)};if(primary)button.Style=(Style)FindResource("PrimaryButton");AutomationProperties.SetName(button,title);button.Click+=delegate{try{action();}catch(Exception e){ShowStatus("Could not complete action",e.Message,true);}};return button;}
        ComboBox Combo(IEnumerable<object> items,int selected,Action changed)
        {var c=new ComboBox{ItemsSource=items.ToArray(),SelectedIndex=selected};c.SelectionChanged+=delegate{if(!suppress&&changed!=null)changed();};return c;}
        ComboBox Strings(IEnumerable<string> items,int selected,Action changed)
        {return Combo(items.Cast<object>(),selected,changed);}
        void Field(StackPanel panel,string title,UIElement input)
        {var group=new StackPanel{Margin=new Thickness(0,0,0,18)};group.Children.Add(new Label{Content=title,Target=input,Foreground=B("Secondary"),FontSize=12,Padding=new Thickness(0,0,0,7)});AutomationProperties.SetName(input,title);group.Children.Add(input);panel.Children.Add(group);}
        WrapPanel Row(params UIElement[] controls){var row=new WrapPanel{Margin=new Thickness(0,7,0,7)};foreach(var c in controls)row.Children.Add(c);return row;}
        void Heading(StackPanel panel,string title,string subtitle){var t=Text(title,25);t.FontWeight=FontWeights.SemiBold;t.Margin=new Thickness(0,0,0,8);panel.Children.Add(t);var sub=Text(subtitle,13,true);sub.Margin=new Thickness(0,0,0,24);panel.Children.Add(sub);}
        void BuildUI()
        {
            if(host!=null&&host.IsRecording)host.StopRecording();
            int selected=tabs==null?0:tabs.SelectedIndex;suppress=true;layouts.Clear();keyButtons.Clear();invalid.Clear();
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new Grid{Margin=new Thickness(26,20,22,14)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var identity=new StackPanel();var title=Text("Lam-Ang Lite",20);title.FontWeight=FontWeights.SemiBold;identity.Children.Add(title);connection=Text("Finding your keyboard…",12,true);connection.Margin=new Thickness(0,5,0,0);identity.Children.Add(connection);header.Children.Add(identity);
            var controls=Row();controls.VerticalAlignment=VerticalAlignment.Center;
            devicePicker=new ComboBox{Width=180,Margin=new Thickness(0,0,10,0)};devicePicker.SelectionChanged+=delegate{if(!suppress){var d=devicePicker.SelectedItem as DeviceInfo;string next=d==null?"":d.Path;if(next!=devicePath){appliedActions=null;deviceState.Clear();}devicePath=next;devicePicker.ToolTip=next;}};AutomationProperties.SetName(devicePicker,"Connected keyboard");controls.Children.Add(devicePicker);
            controls.Children.Add(Button("Refresh",async delegate{await RefreshDevices();}));controls.Children.Add(Button("Save profile",SaveLocal));
            pendingLabel=Text("",12,true);pendingLabel.VerticalAlignment=VerticalAlignment.Center;pendingLabel.Margin=new Thickness(7,0,15,0);controls.Children.Add(pendingLabel);
            applyButton=Button("Apply to keyboard",async delegate{await ApplyPending();},true);controls.Children.Add(applyButton);Grid.SetColumn(controls,1);header.Children.Add(controls);root.Children.Add(header);
            tabs=new TabControl();AddTab("Lighting",BuildLighting());AddTab("Key mapping",BuildMapping());AddTab("Macros",BuildMacros());AddTab("Gaming mode",BuildGaming());AddTab("Profiles",BuildProfiles());Grid.SetRow(tabs,1);root.Children.Add(tabs);tabs.SelectedIndex=Math.Max(0,selected);
            tabs.SelectionChanged+=delegate(object sender,SelectionChangedEventArgs e){if(e.Source==tabs){if(host!=null&&host.IsRecording)host.StopRecording();if(tabs.SelectedIndex==1)ShowAssignment();}};
            var bottom=new Grid{Margin=new Thickness(24,12,24,18),Background=B("Panel")};bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var feedback=new StackPanel{Margin=new Thickness(16,12,16,12)};status=Text("Local profile — not read from the keyboard",13);detail=Text("Choose settings and Apply. Saving a profile does not change the device.",12,true);detail.Margin=new Thickness(0,4,0,0);feedback.Children.Add(status);feedback.Children.Add(detail);bottom.Children.Add(feedback);var actions=Row(Button("Discard",Discard),Button("Diagnostics",Diagnostics),Button("Preferences",Preferences));actions.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(actions,1);bottom.Children.Add(actions);Grid.SetRow(bottom,2);root.Children.Add(bottom);Content=root;
            RefreshMacro();suppress=false;PopulateDevices();UpdatePending();RenderKeys();LayoutEditors();
        }
        void AddTab(string name,UIElement content){tabs.Items.Add(new TabItem{Header=name,Content=content});}
        UIElement Split(StackPanel main,StackPanel inspector)
        {var grid=new Grid();var left=new Border{Padding=new Thickness(27,26,27,20),Child=main};var right=new Border{Padding=new Thickness(22,25,22,20),Background=B("Panel"),BorderBrush=B("Line"),BorderThickness=new Thickness(1,0,0,0),Child=new ScrollViewer{Content=inspector}};grid.Children.Add(left);grid.Children.Add(right);layouts.Add(grid);return new ScrollViewer{Content=grid};}
        void LayoutEditors()
        {
            bool compact=ActualWidth>0&&ActualWidth<1080;
            foreach(var grid in layouts){grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.RowDefinitions.Add(new RowDefinition());if(compact){grid.RowDefinitions[0].Height=GridLength.Auto;grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(grid.Children[1],0);Grid.SetRow(grid.Children[1],1);((FrameworkElement)grid.Children[1]).MaxHeight=550;grid.Height=double.NaN;}else{grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(300)});Grid.SetColumn(grid.Children[1],1);Grid.SetRow(grid.Children[1],0);((FrameworkElement)grid.Children[1]).MaxHeight=double.PositiveInfinity;grid.Height=Math.Max(390,(ActualHeight>0?ActualHeight:Height)-225);}}
        }
        UIElement Keyboard(bool lighting)
        {
            var canvas=new Canvas{Width=786,Height=302};canvas.Children.Add(new Border{Width=776,Height=288,CornerRadius=new CornerRadius(15),Background=B("Panel"),BorderBrush=B("Line"),BorderThickness=new Thickness(1),Margin=new Thickness(5,7,0,0)});
            foreach(var key in DeviceData.Keys)
            {
                var stack=new Grid{Height=27,Width=key.Width*40-12};stack.RowDefinitions.Add(new RowDefinition());stack.RowDefinitions.Add(new RowDefinition{Height=new GridLength(2)});var label=Text(key.Name=="Space"?"":key.Name,key.Width==1&&key.Name.Length>=4?10:12);label.TextWrapping=TextWrapping.NoWrap;label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;stack.Children.Add(label);var rgb=new Border{Height=2,Margin=new Thickness(3,0,3,0),CornerRadius=new CornerRadius(1)};Grid.SetRow(rgb,1);stack.Children.Add(rgb);
                var button=new Button{Content=stack,Width=key.Width*40-5,Height=35,Padding=new Thickness(2,2,2,3),Background=B("Raised"),ToolTip=key.Name,Tag=key,FontSize=12};AutomationProperties.SetName(button,key.Name+" physical key");Canvas.SetLeft(button,25+key.X*40);Canvas.SetTop(button,22+key.Y*40);int usage=key.Usage;
                button.Click+=delegate{selectedUsage=usage;if(lighting&&profile.Effect==20){if(!selection.Add(usage))selection.Remove(usage);UpdateSelection();}else if(!lighting){ShowAssignment();}RenderKeys();};canvas.Children.Add(button);keyButtons.Add(Tuple.Create(button,rgb,usage));
            }
            return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,Margin=new Thickness(0,6,0,16),MaxHeight=330};
        }
        UIElement BuildLighting()
        {
            var main=new StackPanel();var side=new StackPanel();Heading(main,"Lighting","Every original effect, with the controls it supports.");main.Children.Add(Keyboard(true));main.Children.Add(Text("87-key TKL  •  On-screen colors approximate the physical LEDs",11,true));
            selectionLabel=Text("Whole-keyboard effect",13);selectionLabel.Margin=new Thickness(0,20,0,5);main.Children.Add(selectionLabel);
            customControls=new StackPanel();customControls.Children.Add(Row(Button("All",delegate{selection.Clear();foreach(var k in DeviceData.Keys)selection.Add(k.Usage);UpdateSelection();RenderKeys();}),Button("Arrows",delegate{SelectGroup(new[]{79,80,81,82});}),Button("Numbers",delegate{SelectGroup(Enumerable.Range(30,10));}),Button("Invert",delegate{foreach(var k in DeviceData.Keys){if(!selection.Add(k.Usage))selection.Remove(k.Usage);}UpdateSelection();RenderKeys();}),Button("Clear",delegate{selection.Clear();UpdateSelection();RenderKeys();})));
            customControls.Children.Add(Row(Button("Paint color…",delegate{PickPaint();}),Button("Paint selection",delegate{Paint(false);}),Button("Turn selection off",delegate{Paint(true);})));main.Children.Add(customControls);
            main.Children.Add(Text("Changes stay local until Apply to keyboard. Firmware renders the selected effect.",12,true));
            lightingOn=new CheckBox{Content="Lighting on",IsChecked=profile.LightingOn,Margin=new Thickness(0,0,0,17)};lightingOn.Click+=delegate{profile.LightingOn=lightingOn.IsChecked==true;Dirty("lighting");RenderKeys();};side.Children.Add(lightingOn);
            effectPicker=Strings(DeviceData.Effects,profile.Effect-1,delegate{profile.Effect=effectPicker.SelectedIndex+1;UpdateLightControls();Dirty("lighting");});Field(side,"Effect",effectPicker);
            directionControls=new StackPanel();Field(side,"Direction",directionControls);
            colorControls=new StackPanel();fullColor=new CheckBox{Content="Full Color",IsChecked=profile.Lights[profile.Effect-1].FullColor};fullColor.Click+=delegate{profile.Lights[profile.Effect-1].FullColor=fullColor.IsChecked==true;UpdateLightControls();Dirty("lighting");};colorControls.Children.Add(fullColor);
            hex=new TextBox{Text="#0000FF",Tag=0};hex.TextChanged+=delegate{if(suppress)return;Color value;if(TryColor(hex.Text,out value)){invalid.Remove(hex);var s=profile.Lights[profile.Effect-1];s.Red=value.R;s.Green=value.G;s.Blue=value.B;Dirty("lighting");RenderKeys();}else invalid.Add(hex);};colorControls.Children.Add(hex);
            colorControls.Children.Add(Row(Button("Choose color…",PickLightColor)));var swatches=new WrapPanel();foreach(string sample in new[]{"#A8C7FA","#F4E4C8","#D7B8ED","#A9D9C3","#EEAFBE"}){string value=sample;var b=Button("",delegate{SetLightColor((Color)ColorConverter.ConvertFromString(value));});b.Width=30;b.Height=30;b.MinHeight=30;b.Padding=new Thickness(0);b.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(sample));b.ToolTip=sample;AutomationProperties.SetName(b,"Light color "+sample);swatches.Children.Add(b);}colorControls.Children.Add(swatches);Field(side,"Color",colorControls);
            brightnessLabel=Text("",12,true);side.Children.Add(brightnessLabel);brightness=new Slider{Minimum=0,Maximum=15,IsSnapToTickEnabled=true,TickFrequency=1,Value=profile.Lights[profile.Effect-1].Brightness,Margin=new Thickness(0,4,0,20)};brightness.ValueChanged+=delegate{if(!suppress){profile.Lights[profile.Effect-1].Brightness=(int)brightness.Value;Dirty("lighting");RenderKeys();UpdateLightLabels();}};AutomationProperties.SetName(brightness,"Lighting brightness");side.Children.Add(brightness);
            speedLabel=Text("",12,true);side.Children.Add(speedLabel);speed=new Slider{Minimum=0,Maximum=10,IsSnapToTickEnabled=true,TickFrequency=1,Value=5};speed.ValueChanged+=delegate{if(!suppress){profile.Lights[profile.Effect-1].Speed=(int)speed.Value;Dirty("lighting");UpdateLightLabels();}};AutomationProperties.SetName(speed,"Effect speed");side.Children.Add(speed);
            side.Children.Add(Text("Brightness and speed use the keyboard’s native setting levels.",11,true));UpdateLightControls();return Split(main,side);
        }
        void UpdateLightControls()
        {
            if(effectPicker==null||hex==null||brightness==null||speed==null)return;bool old=suppress;suppress=true;var s=profile.Lights[profile.Effect-1];fullColor.IsChecked=s.FullColor;colorControls.Visibility=profile.Effect==20?Visibility.Collapsed:Visibility.Visible;fullColor.IsEnabled=DeviceData.HasColor(profile.Effect);hex.Text=ColorHex(Color.FromRgb((byte)s.Red,(byte)s.Green,(byte)s.Blue));invalid.Remove(hex);hex.IsEnabled=DeviceData.HasColor(profile.Effect)&&!s.FullColor;
            foreach(var child in colorControls.Children.OfType<WrapPanel>())child.IsEnabled=hex.IsEnabled;
            brightness.Maximum=profile.Effect==20?16:15;brightness.Value=s.Brightness;speed.IsEnabled=DeviceData.HasSpeed(profile.Effect);speed.Value=s.Speed;directionControls.Children.Clear();var directions=DeviceData.Directions(profile.Effect);
            if(directions.Length==0)directionControls.Children.Add(Text("Not available for this effect",12,true));else{var row=new WrapPanel();foreach(int d in directions){int value=d;var b=Button(d==0?"Right":d==1?"Left":d==2?"Up":"Down",delegate{profile.Lights[profile.Effect-1].Direction=value;Dirty("lighting");UpdateLightControls();});if(s.Direction==d){b.Background=B("Accent");b.Foreground=B("OnAccent");}row.Children.Add(b);}directionControls.Children.Add(row);}
            if(customControls!=null)customControls.Visibility=profile.Effect==20?Visibility.Visible:Visibility.Collapsed;suppress=old;UpdateLightLabels();RenderKeys();
        }
        void UpdateLightLabels(){if(brightnessLabel==null)return;var s=profile.Lights[profile.Effect-1];int max=profile.Effect==20?16:15;brightnessLabel.Text="Brightness   "+Math.Round(s.Brightness*100.0/max)+"%  ("+s.Brightness+" / "+max+")";speedLabel.Text="Speed   "+s.Speed+" / 10"+(DeviceData.HasSpeed(profile.Effect)?"":"  — not used");}
        static string ColorHex(Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
        static bool TryColor(string text,out Color color){color=Colors.Black;try{if(text==null||text.Length!=7||text[0]!='#')return false;color=Color.FromRgb(byte.Parse(text.Substring(1,2),NumberStyles.HexNumber),byte.Parse(text.Substring(3,2),NumberStyles.HexNumber),byte.Parse(text.Substring(5,2),NumberStyles.HexNumber));return true;}catch(FormatException){return false;}}
        Color? ChooseColor(Color initial){using(var dialog=new System.Windows.Forms.ColorDialog{FullOpen=true,Color=System.Drawing.Color.FromArgb(initial.R,initial.G,initial.B)}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)return Color.FromRgb(dialog.Color.R,dialog.Color.G,dialog.Color.B);}return null;}
        void PickLightColor(){var s=profile.Lights[profile.Effect-1];var value=ChooseColor(Color.FromRgb((byte)s.Red,(byte)s.Green,(byte)s.Blue));if(value.HasValue)SetLightColor(value.Value);}
        void SetLightColor(Color c){var s=profile.Lights[profile.Effect-1];s.FullColor=false;s.Red=c.R;s.Green=c.G;s.Blue=c.B;Dirty("lighting");UpdateLightControls();}
        void PickPaint(){var value=ChooseColor(paint);if(value.HasValue){paint=value.Value;ShowStatus("Paint color selected",ColorHex(paint)+" — select keys, then Paint selection.",false);}}
        void SelectGroup(IEnumerable<int> usages){selection.Clear();foreach(int k in usages)selection.Add(k);UpdateSelection();RenderKeys();}
        void UpdateSelection(){if(selectionLabel!=null)selectionLabel.Text=profile.Effect==20?selection.Count+" keys selected":"Whole-keyboard effect";}
        void Paint(bool off){if(selection.Count==0){ShowStatus("Select keys to paint","Choose keys on the layout, or use All / Arrows / Numbers.",false);return;}foreach(int key in selection){profile.Colors.RemoveAll(c=>c.Usage==key);profile.Colors.Add(new KeyColor{Usage=key,Red=off?0:paint.R,Green=off?0:paint.G,Blue=off?0:paint.B});}Dirty("lighting");RenderKeys();}
        void RenderKeys()
        {
            var light=profile.Lights[profile.Effect-1];foreach(var entry in keyButtons){int usage=entry.Item3;Color color=Color.FromRgb((byte)light.Red,(byte)light.Green,(byte)light.Blue);if(profile.Effect==20){var c=profile.Colors.FirstOrDefault(x=>x.Usage==usage);color=c==null?Colors.Black:Color.FromRgb((byte)c.Red,(byte)c.Green,(byte)c.Blue);}else if(light.FullColor||!DeviceData.HasColor(profile.Effect)){var key=DeviceData.Keys.First(k=>k.Usage==usage);color=Color.FromRgb((byte)(100+key.X/18*130),170,(byte)(230-key.X/18*100));}entry.Item2.Background=new SolidColorBrush(color);entry.Item2.Opacity=profile.LightingOn?Math.Max(.08,light.Brightness/(profile.Effect==20?16.0:15.0)):0;bool selected=(tabs!=null&&tabs.SelectedIndex==1)?usage==selectedUsage:profile.Effect==20&&selection.Contains(usage);entry.Item1.BorderBrush=B(selected?"Accent":"Line");entry.Item1.BorderThickness=new Thickness(selected?2:1);}
        }
        UIElement BuildMapping()
        {
            var main=new StackPanel();var side=new StackPanel();Heading(main,"Key mapping","Select a physical key, choose its action, then stage the assignment.");main.Children.Add(Keyboard(false));main.Children.Add(Text("Assignments are programmed into the keyboard. Program, website, text and profile actions require this app to remain open.",12,true));
            keyTitle=Text(DeviceData.KeyName(selectedUsage)+" key",21);keyTitle.FontWeight=FontWeights.SemiBold;keyTitle.Margin=new Thickness(0,0,0,22);side.Children.Add(keyTitle);
            actionPicker=Strings(KindNames,0,delegate{if(editing!=null){editing.Kind=Kinds[actionPicker.SelectedIndex];BuildAssignmentInputs();}});Field(side,"Assign to",actionPicker);assignmentControls=new StackPanel();side.Children.Add(assignmentControls);side.Children.Add(Row(Button("Stage assignment",StageAssignment,true),Button("Restore key",delegate{profile.Assignments.RemoveAll(a=>a.Usage==selectedUsage);Dirty("mapping");ShowAssignment();})));
            side.Children.Add(Text("Staging edits your draft. Apply to keyboard sends the changed key map.",12,true));ShowAssignment();return Split(main,side);
        }
        void ShowAssignment()
        {if(actionPicker==null)return;var a=profile.Assignments.FirstOrDefault(x=>x.Usage==selectedUsage);editing=a==null?new Assignment{Usage=selectedUsage}:a.Copy();bool old=suppress;suppress=true;keyTitle.Text=DeviceData.KeyName(selectedUsage)+" key";actionPicker.SelectedIndex=Array.IndexOf(Kinds,editing.Kind);suppress=old;BuildAssignmentInputs();RenderKeys();}
        void BuildAssignmentInputs()
        {
            if(assignmentControls==null||editing==null)return;assignmentControls.Children.Clear();destination=null;target=null;modifiers.Clear();chord.Clear();
            if(editing.Kind=="Default"||editing.Kind=="Disabled")return;
            if(editing.Kind=="Keyboard")
            {var keys=DeviceData.Destinations();destination=Combo(keys.Cast<object>(),Math.Max(0,keys.FindIndex(k=>k.Usage==editing.Value)),null);Field(assignmentControls,"New key",destination);var grid=new UniformGrid{Columns=2};string[] labels={"Ctrl","Shift","Alt","Win","Right Ctrl","Right Shift","Right Alt","Right Win"};for(int i=0;i<8;i++){var check=new CheckBox{Content=labels[i],IsChecked=(editing.Modifiers&(1<<i))!=0};modifiers.Add(check);grid.Children.Add(check);}Field(assignmentControls,"Modifiers",grid);}
            else if(editing.Kind=="Chord")
            {var keys=DeviceData.Destinations();for(int i=0;i<3;i++){var options=new List<object>{"None"};options.AddRange(keys.Cast<object>());int value=i<editing.Chord.Length?editing.Chord[i]:0;int index=value==0?0:keys.FindIndex(k=>k.Usage==value)+1;var c=Combo(options,index,null);chord.Add(c);Field(assignmentControls,"Key "+(i+1),c);}}
            else if(editing.Kind=="Macro"){destination=Strings(profile.Macros.Select(m=>m.Name),profile.Macros.Count>0?Math.Min(editing.Value,profile.Macros.Count-1):-1,null);Field(assignmentControls,"Onboard macro",destination);if(profile.Macros.Count==0)assignmentControls.Children.Add(Text("Create and record a macro in Macros first.",12,true));}
            else if(new[]{"Mouse","Media","Windows"}.Contains(editing.Kind)){var names=editing.Kind=="Mouse"?MouseNames:editing.Kind=="Media"?MediaNames:WindowsNames;int index=editing.Kind=="Windows"?Array.IndexOf(WindowsIds,editing.Value):editing.Value-1;destination=Strings(names,Math.Max(0,index),null);Field(assignmentControls,"Action",destination);}
            else
            {if(editing.Kind=="Profile"){var names=ProfileFiles().Select(Path.GetFileNameWithoutExtension).ToList();bool missing=!string.IsNullOrEmpty(editing.Target)&&!names.Contains(editing.Target);if(missing)names.Add(editing.Target);destination=Strings(names,string.IsNullOrEmpty(editing.Target)?0:names.IndexOf(editing.Target),null);Field(assignmentControls,"Local profile",destination);if(missing)assignmentControls.Children.Add(Text("The assigned profile is not saved on this computer: "+editing.Target,12,true));}else{target=new TextBox{Text=editing.Target??"",AcceptsReturn=editing.Kind=="Text",Height=editing.Kind=="Text"?100:double.NaN,TextWrapping=TextWrapping.Wrap};Field(assignmentControls,editing.Kind=="Program"?"Program path":editing.Kind=="Website"?"Website address":"Text",target);if(editing.Kind=="Program")assignmentControls.Children.Add(Button("Browse…",delegate{var dialog=new OpenFileDialog{Filter="Applications (*.exe)|*.exe|All files|*.*"};if(dialog.ShowDialog(this)==true)target.Text=dialog.FileName;}));}}
        }
        void StageAssignment()
        {
            if(editing==null)return;var a=editing;
            if(a.Kind=="Keyboard"){a.Value=((KeyDef)destination.SelectedItem).Usage;a.Modifiers=0;for(int i=0;i<modifiers.Count;i++)if(modifiers[i].IsChecked==true)a.Modifiers|=1<<i;}
            else if(a.Kind=="Chord")a.Chord=chord.Select(c=>c.SelectedItem as KeyDef).Where(k=>k!=null).Select(k=>k.Usage).ToArray();
            else if(a.Kind=="Macro"){if(destination.SelectedIndex<0)throw new ArgumentException("Create a macro first.");a.Value=destination.SelectedIndex;}
            else if(a.Kind=="Windows")a.Value=WindowsIds[destination.SelectedIndex];
            else if(a.Kind=="Mouse"||a.Kind=="Media")a.Value=destination.SelectedIndex+1;
            else if(a.Kind=="Profile"){if(destination.SelectedItem==null)throw new ArgumentException("Save a local profile first.");a.Target=destination.SelectedItem.ToString();}
            else if(target!=null)a.Target=target.Text;
            var candidate=ProfileStore.Clone(profile);candidate.Assignments.RemoveAll(x=>x.Usage==a.Usage);if(a.Kind!="Default")candidate.Assignments.Add(a.Copy());Protocol.Mapping(candidate);profile=candidate;editing=a.Copy();Dirty("mapping");if(a.Kind=="Macro")pending.Add("macros");UpdatePending();ShowStatus("Assignment staged",DeviceData.KeyName(a.Usage)+" → "+KindNames[Array.IndexOf(Kinds,a.Kind)]+". Apply to program the keyboard.",false);RenderKeys();
        }
        Macro CurrentMacro(){return macroPicker!=null&&macroPicker.SelectedIndex>=0&&macroPicker.SelectedIndex<profile.Macros.Count?profile.Macros[macroPicker.SelectedIndex]:null;}
        UIElement BuildMacros()
        {
            var main=new StackPanel();var side=new StackPanel();Heading(main,"Macro editor","Record keyboard and mouse events, edit delays, then assign a macro to a physical key.");
            macroPicker=Strings(profile.Macros.Select(m=>m.Name),profile.Macros.Count>0?0:-1,delegate{if(host!=null)host.StopRecording();RefreshMacro();});Field(main,"Macro",macroPicker);
            recordButton=Button("Start recording",ToggleRecording,true);main.Children.Add(Row(Button("New macro",NewMacro),recordButton,Button("Stop (Esc)",delegate{if(host!=null)host.StopRecording();}),Button("Delete macro",DeleteMacro)));
            eventList=new ListBox{Height=245,Margin=new Thickness(0,12,0,10)};AutomationProperties.SetName(eventList,"Macro event timeline");main.Children.Add(eventList);
            main.Children.Add(Row(Button("Move up",delegate{MoveEvent(-1);}),Button("Move down",delegate{MoveEvent(1);}),Button("Delete event",DeleteEvent),Button("Clear events",delegate{var m=CurrentMacro();if(m!=null){m.Events.Clear();RefreshEvents();Dirty("macros");}})));
            var eventKind=Strings(new[]{"Key down","Key up","Delay","Mouse down","Mouse up","Wheel"},0,null);var eventValue=new TextBox{Text="4"};var editor=new Grid();editor.ColumnDefinitions.Add(new ColumnDefinition());editor.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(95)});eventKind.Margin=new Thickness(0,0,10,0);Grid.SetColumn(eventValue,1);editor.Children.Add(eventKind);editor.Children.Add(eventValue);Field(main,"Event type / value (HID key code, delay ms, button 1–5, wheel ±1)",editor);
            Action<bool> editEvent=delegate(bool replace){var m=CurrentMacro();if(m==null)throw new ArgumentException("Create a macro first.");int value;if(!int.TryParse(eventValue.Text,out value))throw new ArgumentException("Enter a whole-number event value.");var e=new MacroEvent{Kind=eventKind.SelectedItem.ToString(),Value=value};var candidate=ProfileStore.Clone(profile);var list=candidate.Macros[macroPicker.SelectedIndex].Events;if(replace&&eventList.SelectedIndex>=0)list[eventList.SelectedIndex]=e;else list.Add(e);ProfileStore.Validate(candidate);m.Events.Clear();m.Events.AddRange(list);RefreshEvents();Dirty("macros");};
            main.Children.Add(Row(Button("Insert event",delegate{editEvent(false);}),Button("Update selected",delegate{editEvent(true);})));
            eventList.SelectionChanged+=delegate{var e=eventList.SelectedItem as MacroEvent;if(e!=null){eventKind.SelectedItem=e.Kind;eventValue.Text=e.Value.ToString();}};
            main.Children.Add(Text("Recording is global while enabled. Press Esc or Stop to finish. Recorded delays are editable; uploads are bounded by the original 3200-byte transfer format.",12,true));
            macroName=new TextBox{Text=""};macroName.LostFocus+=delegate{var m=CurrentMacro();if(m!=null&&!string.IsNullOrWhiteSpace(macroName.Text)){m.Name=macroName.Text.Trim();RefreshMacroNames();Dirty("macros");}};Field(side,"Macro name",macroName);
            var playback=Strings(new[]{"Once","Repeat","Toggle"},0,null);playback.SelectionChanged+=delegate{var m=CurrentMacro();if(!suppress&&m!=null){m.Playback=playback.SelectedItem.ToString();Dirty("macros");if(profile.Assignments.Any(a=>a.Kind=="Macro"))Dirty("mapping");}};playback.Tag="playback";Field(side,"Playback (Toggle = click to stop)",playback);
            repeats=NumberInput(1,1,255,delegate(int n){var m=CurrentMacro();if(m!=null){m.Repeats=n;Dirty("macros");if(profile.Assignments.Any(a=>a.Kind=="Macro"))Dirty("mapping");}});Field(side,"Repeat count (1–255)",repeats);
            var delayMode=Strings(new[]{"Recorded","Fixed"},0,null);delayMode.SelectionChanged+=delegate{var m=CurrentMacro();if(!suppress&&m!=null){m.DelayMode=delayMode.SelectedItem.ToString();Dirty("macros");}};delayMode.Tag="delaymode";Field(side,"Delay handling",delayMode);
            fixedDelay=NumberInput(100,1,999999,delegate(int n){var m=CurrentMacro();if(m!=null){m.FixedDelay=n;Dirty("macros");}});Field(side,"Fixed delay (ms)",fixedDelay);
            side.Children.Add(Button("Validate macro capacity",delegate{var bytes=Protocol.Macros(profile);ShowStatus("Macros fit the transfer buffer",bytes.Length+" / 3200 bytes, "+profile.Macros.Count+" macro slots.",false);}));
            side.Children.Add(Text("Macros are uploaded to the keyboard. Program/text/profile actions instead need this app running.",12,true));
            side.Tag=new[]{playback,delayMode};RefreshMacro();return Split(main,side);
        }
        TextBox NumberInput(int initial,int min,int max,Action<int> changed)
        {var input=new TextBox{Text=initial.ToString(),Tag=2};input.TextChanged+=delegate{if(suppress)return;int n;if(!int.TryParse(input.Text,out n)||n<min||n>max){invalid.Add(input);input.BorderBrush=B("Attention");}else{invalid.Remove(input);input.BorderBrush=B("Secondary");changed(n);}};return input;}
        void RefreshMacroNames(){if(macroPicker==null)return;int index=macroPicker.SelectedIndex;bool old=suppress;suppress=true;macroPicker.ItemsSource=profile.Macros.Select(m=>m.Name).ToArray();macroPicker.SelectedIndex=Math.Min(index,profile.Macros.Count-1);suppress=old;}
        void RefreshMacro()
        {
            var m=CurrentMacro();if(macroName==null||eventList==null||repeats==null||fixedDelay==null)return;bool old=suppress;suppress=true;macroName.Text=m==null?"":m.Name;repeats.Text=(m==null?1:m.Repeats).ToString();fixedDelay.Text=(m==null?100:m.FixedDelay).ToString();
            foreach(var grid in layouts){var border=grid.Children[1] as Border;var scroll=border==null?null:border.Child as ScrollViewer;var side=scroll==null?null:scroll.Content as StackPanel;var combos=side==null?null:side.Tag as ComboBox[];if(combos!=null){combos[0].SelectedItem=m==null?"Once":m.Playback;combos[1].SelectedItem=m==null?"Recorded":m.DelayMode;}}
            invalid.Remove(repeats);invalid.Remove(fixedDelay);suppress=old;RefreshEvents();
        }
        void RefreshEvents(){var m=CurrentMacro();int index=eventList==null?-1:eventList.SelectedIndex;if(eventList!=null){eventList.ItemsSource=m==null?null:m.Events.ToArray();if(m!=null&&m.Events.Count>0)eventList.SelectedIndex=Math.Max(0,Math.Min(index,m.Events.Count-1));}}
        void NewMacro(){if(host!=null)host.StopRecording();profile.Macros.Add(new Macro{Name="Macro "+(profile.Macros.Count+1)});RefreshMacroNames();macroPicker.SelectedIndex=profile.Macros.Count-1;RefreshMacro();Dirty("macros");}
        void DeleteMacro(){var m=CurrentMacro();if(m==null)return;if(host!=null)host.StopRecording();int slot=macroPicker.SelectedIndex;profile.Macros.RemoveAt(slot);profile.Assignments.RemoveAll(a=>a.Kind=="Macro"&&a.Value==slot);foreach(var a in profile.Assignments.Where(a=>a.Kind=="Macro"&&a.Value>slot))a.Value--;RefreshMacroNames();RefreshMacro();Dirty("macros");Dirty("mapping");}
        void MoveEvent(int delta){var m=CurrentMacro();int from=eventList.SelectedIndex,to=from+delta;if(m==null||from<0||to<0||to>=m.Events.Count)return;var e=m.Events[from];m.Events.RemoveAt(from);m.Events.Insert(to,e);RefreshEvents();eventList.SelectedIndex=to;Dirty("macros");}
        void DeleteEvent(){var m=CurrentMacro();if(m==null||eventList.SelectedIndex<0)return;m.Events.RemoveAt(eventList.SelectedIndex);RefreshEvents();Dirty("macros");}
        void ToggleRecording(){if(host==null)throw new InvalidOperationException("Macro recorder is unavailable.");if(host.IsRecording){host.StopRecording();return;}if(CurrentMacro()==null)NewMacro();var macro=CurrentMacro();if(!MacroEditing.CanStartRecording(macro))throw new ArgumentException("This macro has no room for safely completed recording events. Remove events first.");host.StartRecording();recordingMacro=macro;recordButton.Content="Recording…";ShowStatus("Recording keyboard and mouse","Press Esc or Stop when finished. Nothing is uploaded until Apply.",false);}
        void RecordEvent(MacroEvent e){if(recordingMacro==null||host==null||!host.IsRecording)return;if(!MacroEditing.TryAppendRecording(recordingMacro,e)){host.StopRecording();ShowStatus("Recording stopped","The draft reached its editing limit, including required release events. Remove events or split the macro.",true);return;}RefreshEvents();Dirty("macros");}
        void RecordingStopped()
        {if(recordButton!=null)recordButton.Content="Start recording";if(recordingMacro!=null){int before=recordingMacro.Events.Count;MacroEditing.FinishRecording(recordingMacro);if(recordingMacro.Events.Count!=before)Dirty("macros");recordingMacro=null;RefreshEvents();}ShowStatus("Recording stopped","Review the events and assign the macro to a key before applying.",false);}
        UIElement BuildGaming()
        {
            var main=new StackPanel();var side=new StackPanel();Heading(main,"Gaming mode","Program the original shortcut locks into the keyboard.");var enabled=new CheckBox{Content="Enable gaming mode",IsChecked=profile.GamingEnabled,FontSize=16,Margin=new Thickness(0,4,0,20)};main.Children.Add(enabled);
            var options=new StackPanel{IsEnabled=profile.GamingEnabled};var win=new CheckBox{Content="Disable Windows key",IsChecked=profile.LockWindows};var tab=new CheckBox{Content="Disable Alt + Tab",IsChecked=profile.LockAltTab};var f4=new CheckBox{Content="Disable Alt + F4",IsChecked=profile.LockAltF4};foreach(var c in new[]{win,tab,f4}){c.Margin=new Thickness(0,10,0,10);options.Children.Add(c);}main.Children.Add(options);
            enabled.Click+=delegate{profile.GamingEnabled=enabled.IsChecked==true;options.IsEnabled=profile.GamingEnabled;Dirty("gaming");};win.Click+=delegate{profile.LockWindows=win.IsChecked==true;Dirty("gaming");};tab.Click+=delegate{profile.LockAltTab=tab.IsChecked==true;Dirty("gaming");};f4.Click+=delegate{profile.LockAltF4=f4.IsChecked==true;Dirty("gaming");};
            Heading(side,"On-device controls","Locks use the keyboard’s configuration protocol.");side.Children.Add(Text("Disable gaming mode and Apply to release these locks. The application does not suppress these shortcuts in Windows.",13,true));return Split(main,side);
        }
        IEnumerable<string> ProfileFiles(){return Directory.GetFiles(profilesFolder,"*.json").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase);}
        UIElement BuildProfiles()
        {
            var main=new StackPanel();var side=new StackPanel();Heading(main,"Your profiles","Local profiles contain lighting, key maps, macros and gaming settings.");profileList=new ListBox{Height=300,ItemsSource=ProfileFiles().Select(Path.GetFileNameWithoutExtension).ToArray()};AutomationProperties.SetName(profileList,"Saved profiles");main.Children.Add(profileList);main.Children.Add(Row(Button("Load selected",LoadSelected),Button("New",delegate{if(host!=null)host.StopRecording();revision++;profile=new Profile{Name="New profile"};saved=ProfileStore.Clone(profile);pending.Clear();foreach(string part in new[]{"lighting","mapping","macros","gaming"})pending.Add(part);BuildUI();}),Button("Duplicate",delegate{profile.Name=profile.Name+" copy";BuildUI();SaveLocal();}),Button("Remove",RemoveProfile)));
            main.Children.Add(Row(Button("Import JSON…",ImportProfile),Button("Export JSON…",ExportProfile),Button("Apply complete profile",async delegate{foreach(string s in new[]{"macros","mapping","lighting","gaming"})pending.Add(s);UpdatePending();await ApplyPending();},true)));
            profileName=new TextBox{Text=profile.Name,Tag=4};profileName.TextChanged+=delegate{if(suppress)return;if(string.IsNullOrWhiteSpace(profileName.Text)||profileName.Text.Length>120)invalid.Add(profileName);else{invalid.Remove(profileName);profile.Name=profileName.Text.Trim();}};Field(side,"Profile name",profileName);side.Children.Add(Button("Save local profile",SaveLocal,true));side.Children.Add(Text("Loading or saving never sends device commands. Applying a complete profile replaces the keyboard’s lighting, key assignments, macro table and gaming locks.",13,true));return Split(main,side);
        }
        string SafeName(string name){return new string(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray()).Trim().TrimEnd('.');}
        void SaveLocal(){if(invalid.Count>0)throw new ArgumentException("Correct invalid color, number or profile-name fields before saving.");ProfileStore.Validate(profile);string name=SafeName(profile.Name);if(string.IsNullOrEmpty(name))throw new ArgumentException("Choose a usable profile name.");ProfileStore.Save(Path.Combine(profilesFolder,name+".json"),profile);saved=ProfileStore.Clone(profile);if(profileList!=null)profileList.ItemsSource=ProfileFiles().Select(Path.GetFileNameWithoutExtension).ToArray();ShowStatus("Profile saved locally",profile.Name+" — keyboard settings were not changed.",false);}
        void LoadSelected(){if(profileList.SelectedItem==null)return;LoadProfile(Path.Combine(profilesFolder,profileList.SelectedItem+".json"));}
        void LoadProfile(string path){var next=ProfileStore.Load(path);if(host!=null)host.StopRecording();profile=next;saved=ProfileStore.Clone(profile);pending.Clear();foreach(string s in new[]{"macros","mapping","lighting","gaming"})pending.Add(s);revision++;selection.Clear();BuildUI();ShowStatus("Profile loaded — pending",profile.Name+". Apply to send these settings to the keyboard.",false);}
        void ImportProfile(){var dialog=new OpenFileDialog{Filter="Lam-Ang profiles (*.json)|*.json"};if(dialog.ShowDialog(this)==true)LoadProfile(dialog.FileName);}
        void ExportProfile(){var dialog=new SaveFileDialog{Filter="Lam-Ang profile (*.json)|*.json",FileName=SafeName(profile.Name)+".json"};if(dialog.ShowDialog(this)==true){ProfileStore.Save(dialog.FileName,profile);ShowStatus("Profile exported",dialog.FileName,false);}}
        void RemoveProfile(){if(profileList.SelectedItem==null)return;string path=Path.Combine(profilesFolder,profileList.SelectedItem+".json"),deleted=Path.Combine(DeviceWorker.DataFolder,"Removed profiles");Directory.CreateDirectory(deleted);File.Move(path,Path.Combine(deleted,DateTime.Now.ToString("yyyyMMdd-HHmmss-")+Path.GetFileName(path)));profileList.ItemsSource=ProfileFiles().Select(Path.GetFileNameWithoutExtension).ToArray();ShowStatus("Profile moved to Removed profiles",deleted+" — the keyboard was not changed.",false);}
        void Discard(){if(busy)return;if(host!=null)host.StopRecording();profile=ProfileStore.Clone(saved);pending.Clear();foreach(string part in deviceState.ChangedParts(profile))pending.Add(part);selection.Clear();revision++;BuildUI();ShowStatus("Draft changes discarded","Returned to the saved local profile. "+(pending.Count>0?"It differs from last acknowledged settings; Apply to send it.":"No device commands were sent."),false);}
        void Dirty(string part){if(suppress)return;pending.Add(part);revision++;UpdatePending();ShowStatus("Pending "+string.Join(", ",pending),"Apply to keyboard sends these changes. Ctrl+Enter applies; Ctrl+S saves locally.",false);}
        void UpdatePending(){if(pendingLabel!=null)pendingLabel.Text=pending.Count==0?"No pending changes":pending.Count+" pending section"+(pending.Count==1?"":"s");if(applyButton!=null)applyButton.IsEnabled=!busy&&pending.Count>0;if(devicePicker!=null)devicePicker.IsEnabled=!busy;}
        void ShowStatus(string title,string description,bool error){if(status==null)return;status.Text=title;status.Foreground=B(error?"Attention":"Text");detail.Text=description;}
        void PopulateDevices(){if(devicePicker==null)return;bool old=suppress;suppress=true;devicePicker.ItemsSource=devices.ToArray();devicePicker.SelectedItem=devices.FirstOrDefault(d=>d.Path==devicePath)??devices.FirstOrDefault();var selected=devicePicker.SelectedItem as DeviceInfo;devicePath=selected==null?"":selected.Path;devicePicker.ToolTip=devicePath;connection.Text=selected==null?"Not connected  •  USB required":"Connected  •  "+selected.Manufacturer+"  •  USB HID";suppress=old;}
        async Task RefreshDevices()
        {
            if(refreshing||busy)return;refreshing=true;
            try{var result=await DeviceWorker.Run(new WorkerRequest());if(busy)return;bool had=devices.Count>0;devices=result.Devices??new List<DeviceInfo>();if(!devices.Any(d=>d.Path==devicePath)){appliedActions=null;deviceState.Clear();}PopulateDevices();if(!result.Success)ShowStatus("Device discovery failed",result.Error,true);else if(had&&devices.Count==0)ShowStatus("Keyboard disconnected","Your draft is kept. Reconnect and refresh before applying.",true);}
            catch(Exception e){ShowStatus("Could not refresh device",e.Message,true);}finally{refreshing=false;}
        }
        async Task ApplyPending()
        {
            if(busy||pending.Count==0)return;
            try
            {
                if(invalid.Count>0){var input=invalid.First();tabs.SelectedIndex=input.Tag is int?(int)input.Tag:0;input.Focus();throw new ArgumentException("Correct the highlighted color or number field before applying.");}
                if(host!=null&&host.IsRecording)host.StopRecording();
                var snapshot=ProfileStore.Clone(profile);ProfileStore.Validate(snapshot);var parts=pending.ToArray();if(parts.Contains("mapping")&&snapshot.Assignments.Any(a=>a.Kind=="Macro")&&!parts.Contains("macros"))parts=parts.Concat(new[]{"macros"}).ToArray();
                // Check buffer capacity before starting any device operation.
                if(parts.Contains("macros"))Protocol.Macros(snapshot);if(parts.Contains("mapping"))Protocol.Mapping(snapshot);if(parts.Contains("lighting"))Protocol.Lighting(snapshot);if(parts.Contains("gaming"))Protocol.Gaming(snapshot);
                if(string.IsNullOrEmpty(devicePath))throw new ArgumentException("Connect your Lam-Ang Lite by USB and refresh.");
                busy=true;UpdatePending();int applyingRevision=revision;string applyingDevice=devicePath;if(parts.Contains("mapping"))appliedActions=null;ShowStatus("Applying to keyboard…",string.Join(", ",parts)+". The interface stays responsive while the device worker runs.",false);
                var result=await DeviceWorker.Run(new WorkerRequest{Operation="apply",DevicePath=applyingDevice,Parts=parts,Profile=snapshot});lastLog=result.LogPath;
                if(!string.Equals(devicePath,applyingDevice,StringComparison.OrdinalIgnoreCase)){ShowStatus("Previous-device operation finished","The selected device changed. Its current draft remains pending.",true);return;}
                if(!result.Success){deviceState.Invalidate(parts);ShowStatus("Not applied",result.Error+"  Open Diagnostics for details.",true);return;}
                deviceState.Remember(snapshot,parts);
                if(parts.Contains("mapping")){appliedActions=snapshot;actionContainer=result.Device.ContainerId;}
                if(revision==applyingRevision)foreach(string part in parts)pending.Remove(part);else foreach(string part in deviceState.ChangedParts(profile))pending.Add(part);
                string evidence=result.Writes+" HID writes; "+result.Acknowledgements+" device acknowledgements.";
                ShowStatus("Keyboard acknowledged settings",evidence+(pending.Count>0?" Newer draft edits are still pending.":" "+DateTime.Now.ToString("T")),false);
                try{ProfileStore.Save(Path.Combine(DeviceWorker.DataFolder,"Last acknowledged.json"),snapshot);}catch(Exception e){ShowStatus("Settings acknowledged; history save failed",e.Message,true);}
            }
            catch(Exception e){ShowStatus("Could not apply settings",e.Message,true);}finally{busy=false;UpdatePending();}
        }
        void HostAction(string container,int id)
        {
            if(busy||appliedActions==null||!HidDevice.SamePhysicalDevice(actionContainer,container))return;var a=appliedActions.Assignments.FirstOrDefault(x=>DeviceData.Indices[x.Usage]==id);if(a==null)return;
            try{switch(a.Kind){case "Program":if(!File.Exists(a.Target))throw new FileNotFoundException("Assigned program was not found.",a.Target);Process.Start(new ProcessStartInfo(a.Target){UseShellExecute=true});break;case "Website":Process.Start(new ProcessStartInfo(a.Target){UseShellExecute=true});break;case "Text":HostActions.SendText(a.Target);break;case "Profile":string path=Path.Combine(profilesFolder,SafeName(a.Target)+".json");if(!File.Exists(path))throw new FileNotFoundException("The assigned local profile is missing.",path);ProfileStore.Save(Path.Combine(DeviceWorker.DataFolder,"Recovered drafts",DateTime.Now.ToString("yyyyMMdd-HHmmss-")+Guid.NewGuid().ToString("N")+".json"),profile);LoadProfile(path);var ignored=ApplyPending();break;}}
            catch(Exception e){ShowStatus("Assigned action failed",e.Message,true);}
        }
        void Diagnostics()
        {
            string text="Lam-Ang Studio\r\nTarget: 0C45:8006, MI_00, usage 1:6, feature report 65 bytes\r\n\r\n"+string.Join("\r\n\r\n",devices.Select(d=>d.Manufacturer+" "+d.Product+"\r\n"+d.Path))+"\r\n\r\nLast transaction: "+lastLog;
            if(File.Exists(lastLog))text+="\r\n\r\n"+File.ReadAllText(lastLog);
            var box=new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),Margin=new Thickness(18)};
            new Window{Owner=this,Title="Keyboard diagnostics",Width=850,Height=540,Background=B("Surface"),Content=box,WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show();
        }
        void Preferences()
        {
            if(host!=null&&host.IsRecording)host.StopRecording();var panel=new StackPanel{Margin=new Thickness(24)};Heading(panel,"Preferences","Appearance follows Windows, including high contrast.");
            panel.Children.Add(Button("Refresh system appearance",delegate{ApplyAppearance();BuildUI();}));panel.Children.Add(Button("Interface text color…",delegate{var value=ChooseColor(((SolidColorBrush)B("Text")).Color);if(!value.HasValue)return;double contrast=Contrast(value.Value,((SolidColorBrush)B("Surface")).Color);double second=Contrast(value.Value,((SolidColorBrush)B("Panel")).Color);if(Math.Min(contrast,second)<4.5)throw new ArgumentException("Choose a text color with at least 4.5:1 contrast on both surfaces.");Application.Current.Resources["Text"]=new SolidColorBrush(value.Value);Foreground=B("Text");BuildUI();}));
            panel.Children.Add(Text("Interface language: English. Original effect names are retained. Profiles are stored on this computer; the app does not flash firmware.",13,true));
            panel.Children.Add(Button("Open profile folder",delegate{Process.Start(new ProcessStartInfo(profilesFolder){UseShellExecute=true});}));panel.Children.Add(Button("Stage factory-style defaults",delegate{if(host!=null)host.StopRecording();revision++;profile=new Profile();pending.Clear();foreach(string part in new[]{"lighting","mapping","macros","gaming"})pending.Add(part);BuildUI();ShowStatus("Defaults staged","Apply to reset the programmable settings. No firmware reset command was used.",false);}));
            var window=new Window{Owner=this,Title="Preferences",Width=450,Height=470,Background=B("Surface"),Foreground=B("Text"),Content=panel,WindowStartupLocation=WindowStartupLocation.CenterOwner};panel.Children.Add(Button("Done",delegate{window.Close();}));window.ShowDialog();
        }
        static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return(Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        static double Luminance(Color c){return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
        static double Channel(byte value){double x=value/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
    }
}
