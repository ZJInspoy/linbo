using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using Forms = System.Windows.Forms;

namespace LinboNative
{
    public class LinboState
    {
        public string side { get; set; }
        public Dictionary<string, ViewportState> viewports { get; set; }
        public bool minimapOpen { get; set; }
        public bool organized { get; set; }
        public string organizedSide { get; set; }
        public Dictionary<string, CardSnapshot> organizedSnapshot { get; set; }
        public List<CardModel> cards { get; set; }
        public bool windowPlacementSaved { get; set; }
        public double windowLeft { get; set; }
        public double windowTop { get; set; }
        public double windowWidth { get; set; }
        public double windowHeight { get; set; }
        public string windowMode { get; set; }
        public double windowNormalLeft { get; set; }
        public double windowNormalTop { get; set; }
        public double windowNormalWidth { get; set; }
        public double windowNormalHeight { get; set; }
        public double mainPaneWidth { get; set; }
        public double scratchPaneWidth { get; set; }
        public bool scratchOpen { get; set; }
    }

    public class ViewportState
    {
        public double x { get; set; }
        public double y { get; set; }
        public double scale { get; set; }
    }

    public class CardSnapshot
    {
        public double x { get; set; }
        public double y { get; set; }
        public double w { get; set; }
        public double h { get; set; }
    }

    public class CardModel
    {
        public string id { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double w { get; set; }
        public double h { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string tag { get; set; }
        public bool pinned { get; set; }
        public bool archived { get; set; }
        public double archivedAt { get; set; }
        public CardSnapshot frontSnapshot { get; set; }
        public double createdAt { get; set; }
    }

    public class ScratchItem
    {
        public string id { get; set; }
        public string kind { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double w { get; set; }
        public double h { get; set; }
        public double aspect { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string tag { get; set; }
        public double createdAt { get; set; }
        public BitmapSource image { get; set; }
        public StrokeCollection strokes { get; set; }
    }

    public class TagInfo
    {
        public string Name;
        public Color Color;

        public TagInfo(string name, string color)
        {
            Name = name;
            Color = (Color)ColorConverter.ConvertFromString(color);
        }
    }

    public class MenuAction
    {
        public string Text;
        private readonly Action action;

        public MenuAction(string text, Action action)
        {
            Text = text;
            this.action = action;
        }

        public void Execute()
        {
            if (action != null) action();
        }
    }

    public class LinboApp : Application
    {
        [STAThread]
        public static void Main()
        {
            LinboApp app = new LinboApp();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new LinboWindow());
        }
    }

    public class LinboWindow : Window
    {
        private static readonly Dictionary<string, TagInfo> Tags = new Dictionary<string, TagInfo>
        {
            {"gray", new TagInfo("灰", "#8f938e")},
            {"blue", new TagInfo("蓝", "#83a6c3")},
            {"gold", new TagInfo("金", "#c5a669")},
            {"red", new TagInfo("红", "#b86d67")}
        };

        private readonly string dataDir;
        private readonly string statePath;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private LinboState state;
        private Canvas stage;
        private Canvas world;
        private Canvas overlay;
        private Border selectionBox;
        private Border minimap;
        private Canvas minimapWorld;
        private Rectangle minimapViewport;
        private Button mapButton;
        private Button archiveButton;
        private Button organizeButton;
        private Border archiveCount;
        private TextBlock archiveCountText;
        private Border toast;
        private TextBlock toastText;
        private Border actionMenu;
        private StackPanel actionMenuItems;
        private Border windowChrome;
        private Grid windowChromeSurface;
        private Dictionary<string, CardControl> cardControls = new Dictionary<string, CardControl>();
        private Dictionary<string, ScratchItemControl> scratchControls = new Dictionary<string, ScratchItemControl>();
        private List<ScratchItem> scratchItems = new List<ScratchItem>();
        private ViewportState scratchViewport = new ViewportState { x = 0, y = 0, scale = 1 };
        private Border scratchPane;
        private Canvas scratchWorld;
        private Canvas scratchOverlay;
        private TransformGroup scratchWorldTransform;
        private ScaleTransform scratchScale;
        private TranslateTransform scratchTranslate;
        private Border scratchMinimap;
        private Canvas scratchMinimapWorld;
        private Rectangle scratchMinimapViewport;
        private MinimapMetrics scratchMinimapMetrics;
        private bool scratchMinimapDragging;
        private Point scratchMinimapDragOffset;
        private double mainPaneWidth = 585;
        private double scratchPaneWidth = 585;
        private Border scratchPaneResizeGrip;
        private Border scratchSplitResizeGrip;
        private Border windowResizeLeftGrip;
        private Border windowResizeRightGrip;
        private Border windowResizeTopGrip;
        private Border windowResizeBottomGrip;
        private TransformGroup worldTransform;
        private ScaleTransform worldScale;
        private TranslateTransform worldTranslate;
        private DispatcherTimer saveTimer;
        private DispatcherTimer toastTimer;
        private string selectedId;
        private double zCounter = 1;
        private bool renderingBackdrop;

        private string interaction;
        private string interactionCardId;
        private Point pointerStart;
        private Point selectionStart;
        private Point cardStart;
        private Size cardSizeStart;
        private ViewportState viewportStart;
        private bool minimapDragging;
        private MinimapMetrics minimapMetrics;
        private Point minimapDragOffset;
        private bool scratchOpen;
        private bool spiritVisible;
        private Button spiritButton;
        private Border scratchToolbar;
        private Slider scratchBrushSlider;
        private Color scratchInkColor = Color.FromRgb(232, 232, 226);
        private Point scratchItemStart;
        private Size scratchItemSizeStart;
        private string selectedScratchItemId;
        private string pendingScratchDrawingItemId;
        private string drawingScratchItemId;
        private string scratchResizeMode;
        private string windowResizeMode;
        private Rect windowResizeStart;
        private double mainPaneWidthStart;
        private double scratchPaneWidthStart;
        private string windowMode = "normal";
        private Rect normalWindowRect = new Rect(0, 0, 585, 1040);
        private bool applyingWindowGeometry;

        public LinboWindow()
        {
            string dataOverride = Environment.GetEnvironmentVariable("LINBO_DATA_DIR");
            dataDir = String.IsNullOrWhiteSpace(dataOverride)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Linbo")
                : dataOverride;
            statePath = System.IO.Path.Combine(dataDir, "linbo-native-state.json");
            state = LoadState();
            mainPaneWidth = Clamp(state.mainPaneWidth > 0 ? state.mainPaneWidth : 585, 360, 1800);
            scratchPaneWidth = Clamp(state.scratchPaneWidth > 0 ? state.scratchPaneWidth : mainPaneWidth, 320, 1800);
            scratchOpen = state.scratchOpen;
            windowMode = String.IsNullOrEmpty(state.windowMode) ? "normal" : state.windowMode;
            normalWindowRect = new Rect(
                state.windowNormalWidth > 0 ? state.windowNormalLeft : state.windowLeft,
                state.windowNormalHeight > 0 ? state.windowNormalTop : state.windowTop,
                state.windowNormalWidth > 0 ? state.windowNormalWidth : (scratchOpen ? mainPaneWidth + scratchPaneWidth : mainPaneWidth),
                state.windowNormalHeight > 0 ? state.windowNormalHeight : 1040);

            Title = "泠波";
            Width = state.windowWidth > 0 ? state.windowWidth : (scratchOpen ? mainPaneWidth + scratchPaneWidth : mainPaneWidth);
            Height = state.windowHeight > 0 ? state.windowHeight : 1040;
            MinWidth = 360;
            MinHeight = 640;
            WindowStartupLocation = state.windowPlacementSaved ? WindowStartupLocation.Manual : WindowStartupLocation.CenterScreen;
            if (state.windowPlacementSaved)
            {
                Left = state.windowLeft;
                Top = state.windowTop;
            }
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = BrushFrom("#030304");
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
            Icon = LoadIconImage();
            AllowDrop = true;

            BuildUi();
            ApplySavedWindowMode();
            Render();
            Loaded += delegate { stage.Focus(); };
            LocationChanged += delegate { if (!applyingWindowGeometry) SaveStateSoon(); };
            SizeChanged += delegate { if (!applyingWindowGeometry) SaveStateSoon(); };
            Closing += delegate { SaveWindowPlacementToState(); SaveStateNow(); };
        }

        private ImageSource LoadIconImage()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "linbo-icon.png");
            if (!File.Exists(path)) return null;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            return image;
        }

        private ImageSource LoadSpiritImage()
        {
            string assetPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "mist-spirit.jpg");
            string rootPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "小图标.jpg");
            string path = File.Exists(assetPath) ? assetPath : rootPath;
            if (!File.Exists(path)) return null;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void BuildUi()
        {
            stage = new Canvas();
            stage.ClipToBounds = true;
            stage.Focusable = true;
            stage.Background = new LinearGradientBrush(BrushFrom("#050506").Color, BrushFrom("#030304").Color, 90);
            Content = stage;

            world = new Canvas();
            world.Width = 1;
            world.Height = 1;
            worldTransform = new TransformGroup();
            worldScale = new ScaleTransform(1, 1);
            worldTranslate = new TranslateTransform(0, 0);
            worldTransform.Children.Add(worldScale);
            worldTransform.Children.Add(worldTranslate);
            world.RenderTransform = worldTransform;
            stage.Children.Add(world);

            overlay = new Canvas();
            stage.Children.Add(overlay);

            selectionBox = new Border();
            selectionBox.BorderBrush = Solid(233, 229, 220, 184);
            selectionBox.BorderThickness = new Thickness(1);
            selectionBox.Background = Solid(233, 229, 220, 20);
            selectionBox.CornerRadius = new CornerRadius(8);
            selectionBox.Visibility = Visibility.Collapsed;
            overlay.Children.Add(selectionBox);

            AddScratchPane();
            AddBrand();
            AddDockButtons();
            AddScratchToolbar();
            AddMinimap();
            AddSpiritButton();
            AddToast();
            AddActionMenu();
            AddWindowChrome();
            AddWindowResizeGrips();

            stage.MouseLeftButtonDown += BeginSelection;
            stage.MouseMove += StageMouseMove;
            stage.MouseLeftButtonUp += StageMouseLeftButtonUp;
            stage.MouseUp += StageMouseUp;
            stage.MouseWheel += StageMouseWheel;
            stage.MouseDown += StageMouseDown;
            stage.MouseRightButtonUp += StageRightClick;
            stage.DragEnter += StageDragEnter;
            stage.DragOver += StageDragEnter;
            stage.DragLeave += delegate { };
            stage.Drop += StageDrop;
            SizeChanged += delegate { SyncPaneWidthsToWindow(); PositionFloatingUi(); RenderMinimap(); PositionWindowChrome(); };
            PreviewKeyDown += WindowPreviewKeyDown;
            PreviewKeyUp += WindowPreviewKeyUp;
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteCommand));

            saveTimer = new DispatcherTimer();
            saveTimer.Interval = TimeSpan.FromMilliseconds(140);
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveStateNow(); };
            toastTimer = new DispatcherTimer();
            toastTimer.Interval = TimeSpan.FromSeconds(1.8);
            toastTimer.Tick += delegate { toastTimer.Stop(); toast.Visibility = Visibility.Collapsed; };
        }

        private void AddBrand()
        {
            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.Margin = new Thickness(18, 16, 0, 0);
            brand.HorizontalAlignment = HorizontalAlignment.Left;
            brand.VerticalAlignment = VerticalAlignment.Top;
            brand.IsHitTestVisible = false;
            brand.Opacity = 0.72;

            ImageSource icon = LoadIconImage();
            if (icon != null)
            {
                Image image = new Image();
                image.Source = icon;
                image.Width = 22;
                image.Height = 22;
                image.Stretch = Stretch.UniformToFill;
                brand.Children.Add(image);
            }

            TextBlock name = new TextBlock();
            name.Text = "泠波";
            name.Foreground = Solid(248, 248, 244, 184);
            name.FontSize = 14;
            name.Margin = new Thickness(8, 1, 0, 0);
            brand.Children.Add(name);
            Panel.SetZIndex(brand, 18);
            stage.Children.Add(brand);
        }

        private void AddScratchPane()
        {
            scratchPane = new Border();
            scratchPane.Width = MainPaneWidth();
            scratchPane.Background = BrushFrom("#030304");
            scratchPane.BorderBrush = Solid(255, 255, 255, 18);
            scratchPane.BorderThickness = new Thickness(1, 0, 0, 0);
            scratchPane.ClipToBounds = true;
            scratchPane.Visibility = Visibility.Collapsed;
            Panel.SetZIndex(scratchPane, 19);

            Canvas paneCanvas = new Canvas();
            paneCanvas.Focusable = true;
            scratchPane.Child = paneCanvas;

            scratchWorld = new Canvas();
            scratchWorld.Width = 1;
            scratchWorld.Height = 1;
            scratchWorldTransform = new TransformGroup();
            scratchScale = new ScaleTransform(1, 1);
            scratchTranslate = new TranslateTransform(0, 0);
            scratchWorldTransform.Children.Add(scratchScale);
            scratchWorldTransform.Children.Add(scratchTranslate);
            scratchWorld.RenderTransform = scratchWorldTransform;
            paneCanvas.Children.Add(scratchWorld);

            scratchOverlay = new Canvas();
            paneCanvas.Children.Add(scratchOverlay);

            AddScratchMinimap(paneCanvas);
            stage.Children.Add(scratchPane);

            scratchSplitResizeGrip = new Border();
            scratchSplitResizeGrip.Width = 12;
            scratchSplitResizeGrip.Background = Brushes.Transparent;
            scratchSplitResizeGrip.Cursor = Cursors.SizeWE;
            scratchSplitResizeGrip.Visibility = Visibility.Collapsed;
            scratchSplitResizeGrip.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                BeginPaneResize("pane-split-resize", e);
            };
            Panel.SetZIndex(scratchSplitResizeGrip, 56);
            stage.Children.Add(scratchSplitResizeGrip);

            scratchPaneResizeGrip = new Border();
            scratchPaneResizeGrip.Width = 12;
            scratchPaneResizeGrip.Background = Brushes.Transparent;
            scratchPaneResizeGrip.Cursor = Cursors.SizeWE;
            scratchPaneResizeGrip.Visibility = Visibility.Collapsed;
            scratchPaneResizeGrip.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                BeginPaneResize("scratch-pane-resize", e);
            };
            Panel.SetZIndex(scratchPaneResizeGrip, 55);
            stage.Children.Add(scratchPaneResizeGrip);
        }

        private void AddScratchMinimap(Canvas paneCanvas)
        {
            scratchMinimap = new Border();
            scratchMinimap.Width = 148;
            scratchMinimap.Height = 108;
            scratchMinimap.CornerRadius = new CornerRadius(8);
            scratchMinimap.BorderBrush = Solid(255, 255, 255, 31);
            scratchMinimap.BorderThickness = new Thickness(1);
            scratchMinimap.Background = new SolidColorBrush(Color.FromArgb(168, 12, 12, 13));
            scratchMinimap.Cursor = Cursors.Hand;
            Panel.SetZIndex(scratchMinimap, 44);

            Canvas grid = new Canvas();
            scratchMinimapWorld = new Canvas();
            scratchMinimapWorld.Width = scratchMinimap.Width;
            scratchMinimapWorld.Height = scratchMinimap.Height;
            scratchMinimapViewport = new Rectangle();
            scratchMinimapViewport.Stroke = Solid(255, 255, 255, 194);
            scratchMinimapViewport.StrokeThickness = 1;
            scratchMinimapViewport.RadiusX = 4;
            scratchMinimapViewport.RadiusY = 4;
            scratchMinimapViewport.Fill = Solid(255, 255, 255, 10);
            grid.Children.Add(scratchMinimapWorld);
            grid.Children.Add(scratchMinimapViewport);
            scratchMinimap.Child = grid;
            scratchMinimap.MouseLeftButtonDown += ScratchMinimapMouseDown;
            scratchMinimap.MouseMove += ScratchMinimapMouseMove;
            scratchMinimap.MouseLeftButtonUp += delegate { StopScratchMinimapDrag(); };
            scratchMinimap.LostMouseCapture += delegate { StopScratchMinimapDrag(); };
            paneCanvas.Children.Add(scratchMinimap);
        }

        private void AddDockButtons()
        {
            mapButton = CreateDockButton("map");
            archiveButton = CreateDockButton("archive");
            organizeButton = CreateDockButton("organize");
            mapButton.Click += delegate { state.minimapOpen = !state.minimapOpen; StopMinimapDrag(true); SaveStateSoon(); Render(); };
            archiveButton.Click += delegate { FlipSide(IsArchiveSide() ? "front" : "archive"); };
            organizeButton.Click += delegate { OrganizeCards(); };
            stage.Children.Add(mapButton);
            stage.Children.Add(archiveButton);
            stage.Children.Add(organizeButton);

            archiveCountText = new TextBlock();
            archiveCountText.Foreground = Brushes.White;
            archiveCountText.FontSize = 10;
            archiveCountText.TextAlignment = TextAlignment.Center;
            archiveCountText.VerticalAlignment = VerticalAlignment.Center;
            archiveCount = new Border();
            archiveCount.Background = Solid(184, 109, 103, 235);
            archiveCount.CornerRadius = new CornerRadius(8);
            archiveCount.Width = 18;
            archiveCount.Height = 16;
            archiveCount.Child = archiveCountText;
            archiveCount.Visibility = Visibility.Collapsed;
            stage.Children.Add(archiveCount);
            PositionFloatingUi();
        }

        private void AddScratchToolbar()
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Margin = new Thickness(12, 10, 12, 10);
            row.VerticalAlignment = VerticalAlignment.Center;

            scratchBrushSlider = new Slider();
            scratchBrushSlider.Width = 112;
            scratchBrushSlider.Minimum = 2;
            scratchBrushSlider.Maximum = 28;
            scratchBrushSlider.Value = 8;
            scratchBrushSlider.Margin = new Thickness(0, 0, 10, 0);
            scratchBrushSlider.ValueChanged += delegate { UpdateScratchDrawingAttributes(); };
            row.Children.Add(scratchBrushSlider);

            row.Children.Add(CreateInkSwatch(Color.FromRgb(232, 232, 226)));
            row.Children.Add(CreateInkSwatch(Color.FromRgb(110, 156, 202)));
            row.Children.Add(CreateInkSwatch(Color.FromRgb(220, 182, 86)));
            row.Children.Add(CreateInkSwatch(Color.FromRgb(196, 88, 86)));

            scratchToolbar = new Border();
            scratchToolbar.CornerRadius = new CornerRadius(12);
            scratchToolbar.BorderBrush = Solid(180, 220, 255, 44);
            scratchToolbar.BorderThickness = new Thickness(1);
            scratchToolbar.Background = Solid(8, 10, 12, 226);
            scratchToolbar.Child = row;
            scratchToolbar.Visibility = Visibility.Collapsed;
            scratchToolbar.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.46, BlurRadius = 34, ShadowDepth = 10 };
            Panel.SetZIndex(scratchToolbar, 86);
            stage.Children.Add(scratchToolbar);
        }

        private Button CreateInkSwatch(Color color)
        {
            Button button = new Button();
            button.Width = 22;
            button.Height = 22;
            button.Margin = new Thickness(0, 0, 6, 0);
            button.Background = new SolidColorBrush(color);
            button.BorderBrush = Solid(255, 255, 255, 34);
            button.BorderThickness = new Thickness(1);
            button.Template = RoundButtonTemplate(11);
            button.Click += delegate
            {
                scratchInkColor = color;
                UpdateScratchDrawingAttributes();
                if (!String.IsNullOrEmpty(pendingScratchDrawingItemId)) StartPendingScratchDrawing();
                else if (!String.IsNullOrEmpty(drawingScratchItemId) && scratchToolbar != null) scratchToolbar.Visibility = Visibility.Collapsed;
            };
            return button;
        }

        private void AddSpiritButton()
        {
            spiritButton = new Button();
            spiritButton.Width = 68;
            spiritButton.Height = 68;
            spiritButton.Padding = new Thickness(0);
            spiritButton.BorderThickness = new Thickness(0);
            spiritButton.Background = Brushes.Transparent;
            spiritButton.Cursor = Cursors.Hand;
            spiritButton.Visibility = Visibility.Collapsed;
            spiritButton.Opacity = 0;
            spiritButton.RenderTransform = new TranslateTransform();
            spiritButton.Template = RoundButtonTemplate(34);
            ImageSource source = LoadSpiritImage();
            if (source != null)
            {
                Image image = new Image();
                image.Source = source;
                image.Stretch = Stretch.UniformToFill;
                image.Width = 68;
                image.Height = 68;
                image.Opacity = 0.92;
                spiritButton.Content = image;
            }
            spiritButton.Click += delegate { ToggleScratchCanvas(); };
            Panel.SetZIndex(spiritButton, 66);
            stage.Children.Add(spiritButton);
            PositionFloatingUi();
        }

        private Button CreateDockButton(string kind)
        {
            Button button = new Button();
            button.Width = 42;
            button.Height = 42;
            button.BorderThickness = new Thickness(1);
            button.BorderBrush = Solid(255, 255, 255, 31);
            button.Background = Solid(255, 255, 255, 20);
            button.Foreground = Solid(255, 255, 255, 210);
            button.Padding = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.Template = RoundButtonTemplate(21);
            Panel.SetZIndex(button, 28);
            button.Content = CreateDockIcon(kind);
            return button;
        }

        private UIElement CreateDockIcon(string kind)
        {
            Canvas canvas = new Canvas();
            canvas.Width = 18;
            canvas.Height = 18;
            Brush stroke = Solid(255, 255, 255, 210);
            if (kind == "map")
            {
                Rectangle outer = new Rectangle();
                outer.Width = 16;
                outer.Height = 16;
                outer.RadiusX = 4;
                outer.RadiusY = 4;
                outer.Stroke = stroke;
                outer.StrokeThickness = 1.5;
                Canvas.SetLeft(outer, 1);
                Canvas.SetTop(outer, 1);
                canvas.Children.Add(outer);
                Rectangle inner = new Rectangle();
                inner.Width = 7;
                inner.Height = 7;
                inner.RadiusX = 2;
                inner.RadiusY = 2;
                inner.Stroke = Solid(255, 255, 255, 158);
                inner.StrokeThickness = 1.5;
                Canvas.SetLeft(inner, 6);
                Canvas.SetTop(inner, 4);
                canvas.Children.Add(inner);
            }
            else if (kind == "archive")
            {
                Ellipse ellipse = new Ellipse();
                ellipse.Width = 12;
                ellipse.Height = 12;
                ellipse.Stroke = stroke;
                ellipse.StrokeThickness = 1.5;
                Canvas.SetLeft(ellipse, 3);
                Canvas.SetTop(ellipse, 3);
                canvas.Children.Add(ellipse);
                Line line = new Line();
                line.X1 = 3;
                line.Y1 = 10;
                line.X2 = 12;
                line.Y2 = 6;
                line.Stroke = stroke;
                line.StrokeThickness = 1.5;
                canvas.Children.Add(line);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    Rectangle r = new Rectangle();
                    r.Width = i == 2 ? 8 : 12;
                    r.Height = 3;
                    r.RadiusX = 1.5;
                    r.RadiusY = 1.5;
                    r.Fill = i == 1 ? Solid(255, 255, 255, 178) : stroke;
                    Canvas.SetLeft(r, 3);
                    Canvas.SetTop(r, 4 + i * 6);
                    canvas.Children.Add(r);
                }
            }
            return canvas;
        }

        private ControlTemplate RoundButtonTemplate(double radius)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        private void AddMinimap()
        {
            minimap = new Border();
            minimap.Width = 176;
            minimap.Height = 126;
            minimap.CornerRadius = new CornerRadius(8);
            minimap.BorderBrush = Solid(255, 255, 255, 31);
            minimap.BorderThickness = new Thickness(1);
            minimap.Background = new SolidColorBrush(Color.FromArgb(184, 12, 12, 13));
            minimap.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.42, BlurRadius = 30, ShadowDepth = 8 };
            minimap.Visibility = Visibility.Collapsed;
            minimap.Cursor = Cursors.Hand;
            Panel.SetZIndex(minimap, 24);

            Canvas grid = new Canvas();
            minimapWorld = new Canvas();
            minimapWorld.Width = 176;
            minimapWorld.Height = 126;
            minimapViewport = new Rectangle();
            minimapViewport.Stroke = Solid(255, 255, 255, 199);
            minimapViewport.StrokeThickness = 1;
            minimapViewport.RadiusX = 4;
            minimapViewport.RadiusY = 4;
            minimapViewport.Fill = Solid(255, 255, 255, 10);
            grid.Children.Add(minimapWorld);
            grid.Children.Add(minimapViewport);
            minimap.Child = grid;
            minimap.MouseLeftButtonDown += MinimapMouseDown;
            minimap.MouseMove += MinimapMouseMove;
            minimap.MouseLeftButtonUp += delegate { StopMinimapDrag(true); };
            minimap.LostMouseCapture += delegate { StopMinimapDrag(true); };
            stage.Children.Add(minimap);
            PositionFloatingUi();
        }

        private void AddToast()
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Margin = new Thickness(14, 0, 12, 0);
            TextBlock check = new TextBlock();
            check.Text = "✓";
            check.FontSize = 14;
            check.Width = 18;
            check.Height = 18;
            check.TextAlignment = TextAlignment.Center;
            check.Foreground = Brushes.White;
            row.Children.Add(check);
            toastText = new TextBlock();
            toastText.Text = "复制成功";
            toastText.Foreground = Brushes.White;
            toastText.FontSize = 14;
            toastText.Margin = new Thickness(8, 0, 8, 0);
            row.Children.Add(toastText);

            toast = new Border();
            toast.Height = 42;
            toast.MinWidth = 150;
            toast.CornerRadius = new CornerRadius(10);
            toast.Background = Solid(57, 137, 51, 235);
            toast.Child = row;
            toast.Visibility = Visibility.Collapsed;
            toast.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.34, BlurRadius = 28, ShadowDepth = 10 };
            Panel.SetZIndex(toast, 70);
            stage.Children.Add(toast);
            PositionFloatingUi();
        }

        private void AddActionMenu()
        {
            actionMenuItems = new StackPanel();
            actionMenuItems.Margin = new Thickness(6);
            actionMenu = new Border();
            actionMenu.MinWidth = 118;
            actionMenu.CornerRadius = new CornerRadius(8);
            actionMenu.BorderThickness = new Thickness(1);
            actionMenu.BorderBrush = Solid(255, 255, 255, 31);
            actionMenu.Background = Solid(18, 18, 19, 214);
            actionMenu.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.48, BlurRadius = 32, ShadowDepth = 8 };
            actionMenu.Child = actionMenuItems;
            actionMenu.Visibility = Visibility.Collapsed;
            Panel.SetZIndex(actionMenu, 80);
            stage.Children.Add(actionMenu);
        }

        private void AddWindowChrome()
        {
            windowChrome = new Border();
            windowChrome.Height = 42;
            windowChrome.Background = Solid(0, 0, 0, 1);
            windowChrome.Cursor = Cursors.Arrow;
            Panel.SetZIndex(windowChrome, 120);

            windowChromeSurface = new Grid();
            windowChromeSurface.Opacity = 0;
            windowChromeSurface.Background = Solid(12, 12, 13, 226);
            windowChromeSurface.ColumnDefinitions.Add(new ColumnDefinition());
            windowChromeSurface.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            ImageSource icon = LoadIconImage();
            if (icon != null)
            {
                Image image = new Image { Source = icon, Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0) };
                title.Children.Add(image);
            }
            title.Children.Add(new TextBlock { Text = "泠波", Foreground = Solid(248, 248, 244, 205), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            windowChromeSurface.Children.Add(title);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(buttons, 1);
            buttons.Children.Add(WindowButton("一", delegate { WindowState = WindowState.Minimized; }));
            buttons.Children.Add(WindowButton("□", delegate { CycleWindowMaxMode(); }));
            buttons.Children.Add(WindowButton("×", delegate { Close(); }));
            windowChromeSurface.Children.Add(buttons);

            windowChrome.Child = windowChromeSurface;
            windowChrome.MouseEnter += delegate { FadeChrome(1); };
            windowChrome.MouseLeave += delegate { FadeChrome(0); };
            windowChrome.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource is Button) return;
                if (e.ClickCount >= 2)
                {
                    CycleWindowMaxMode();
                    return;
                }
                try { DragMove(); } catch { }
            };
            stage.Children.Add(windowChrome);
            PositionWindowChrome();
        }

        private void AddWindowResizeGrips()
        {
            windowResizeLeftGrip = CreateWindowResizeGrip(Cursors.SizeWE, "window-left-resize");
            windowResizeRightGrip = CreateWindowResizeGrip(Cursors.SizeWE, "window-right-resize");
            windowResizeTopGrip = CreateWindowResizeGrip(Cursors.SizeNS, "window-top-resize");
            windowResizeBottomGrip = CreateWindowResizeGrip(Cursors.SizeNS, "window-bottom-resize");
            stage.Children.Add(windowResizeLeftGrip);
            stage.Children.Add(windowResizeRightGrip);
            stage.Children.Add(windowResizeTopGrip);
            stage.Children.Add(windowResizeBottomGrip);
        }

        private Border CreateWindowResizeGrip(Cursor cursor, string mode)
        {
            Border grip = new Border();
            grip.Background = Brushes.Transparent;
            grip.Cursor = cursor;
            Panel.SetZIndex(grip, 121);
            grip.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                BeginWindowResize(mode, e);
            };
            return grip;
        }

        private void BeginPaneResize(string mode, MouseButtonEventArgs e)
        {
            HideMenus();
            interaction = mode;
            pointerStart = e.GetPosition(stage);
            mainPaneWidthStart = MainPaneWidth();
            scratchPaneWidthStart = ScratchPaneWidth();
            stage.CaptureMouse();
            e.Handled = true;
        }

        private void BeginWindowResize(string mode, MouseButtonEventArgs e)
        {
            HideMenus();
            if (windowMode != "normal")
            {
                windowMode = "normal";
                normalWindowRect = new Rect(Left, Top, Width, Height);
            }
            interaction = "window-resize";
            windowResizeMode = mode;
            pointerStart = e.GetPosition(stage);
            windowResizeStart = new Rect(Left, Top, Width, Height);
            mainPaneWidthStart = MainPaneWidth();
            scratchPaneWidthStart = ScratchPaneWidth();
            stage.CaptureMouse();
            e.Handled = true;
        }

        private void CycleWindowMaxMode()
        {
            if (windowMode == "normal")
            {
                CaptureNormalWindowRect();
                ApplyVerticalMaximize();
            }
            else if (windowMode == "vertical")
            {
                ApplyFullMaximize();
            }
            else
            {
                RestoreNormalWindowRect();
            }
            SaveStateSoon();
        }

        private void CaptureNormalWindowRect()
        {
            if (Width > 0 && Height > 0) normalWindowRect = new Rect(Left, Top, Width, Height);
        }

        private void ApplyVerticalMaximize()
        {
            Rect work = SystemParameters.WorkArea;
            applyingWindowGeometry = true;
            try
            {
                Left = normalWindowRect.Left;
                Width = normalWindowRect.Width;
                Top = work.Top;
                Height = work.Height;
                windowMode = "vertical";
            }
            finally { applyingWindowGeometry = false; }
            PositionFloatingUi();
            RenderMinimap();
            RenderScratchMinimap();
        }

        private void ApplyFullMaximize()
        {
            Rect work = SystemParameters.WorkArea;
            applyingWindowGeometry = true;
            try
            {
                Left = work.Left;
                Top = work.Top;
                Width = work.Width;
                Height = work.Height;
                FitPanesToTotalWidth(work.Width);
                windowMode = "full";
            }
            finally { applyingWindowGeometry = false; }
            PositionFloatingUi();
            RenderMinimap();
            RenderScratchMinimap();
        }

        private void RestoreNormalWindowRect()
        {
            applyingWindowGeometry = true;
            try
            {
                Left = normalWindowRect.Left;
                Top = normalWindowRect.Top;
                Width = normalWindowRect.Width;
                Height = normalWindowRect.Height;
                FitPanesToTotalWidth(normalWindowRect.Width);
                windowMode = "normal";
            }
            finally { applyingWindowGeometry = false; }
            PositionFloatingUi();
            RenderMinimap();
            RenderScratchMinimap();
        }

        private Button WindowButton(string text, RoutedEventHandler click)
        {
            Button button = new Button();
            button.Content = text;
            button.Width = 42;
            button.Height = 30;
            button.Margin = new Thickness(2, 0, 0, 0);
            button.BorderThickness = new Thickness(0);
            button.Background = Brushes.Transparent;
            button.Foreground = Solid(248, 248, 244, 214);
            button.Template = FlatMenuButtonTemplate();
            button.Click += click;
            return button;
        }

        private void FadeChrome(double to)
        {
            if (windowChromeSurface == null) return;
            windowChromeSurface.BeginAnimation(OpacityProperty, new DoubleAnimation(windowChromeSurface.Opacity, to, TimeSpan.FromMilliseconds(140)));
        }

        private void PositionWindowChrome()
        {
            if (windowChrome == null) return;
            windowChrome.Width = ActualStageWidth();
            Canvas.SetLeft(windowChrome, 0);
            Canvas.SetTop(windowChrome, 0);
        }

        private void PositionFloatingUi()
        {
            if (mapButton == null) return;
            double width = MainPaneWidth();
            double height = StageHeight();
            Canvas.SetLeft(mapButton, 16);
            Canvas.SetTop(mapButton, height - 58);
            if (archiveButton != null)
            {
                Canvas.SetLeft(archiveButton, (width - 42) / 2);
                Canvas.SetTop(archiveButton, height - 58);
            }
            if (organizeButton != null)
            {
                Canvas.SetLeft(organizeButton, width - 58);
                Canvas.SetTop(organizeButton, height - 58);
            }
            if (archiveCount != null)
            {
                Canvas.SetLeft(archiveCount, (width - 42) / 2 + 28);
                Canvas.SetTop(archiveCount, height - 62);
            }
            if (minimap != null)
            {
                Canvas.SetLeft(minimap, 16);
                Canvas.SetTop(minimap, height - 194);
            }
            if (toast != null)
            {
                toast.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(toast, (width - toast.DesiredSize.Width) / 2);
                Canvas.SetTop(toast, 18);
            }
            if (spiritButton != null)
            {
                Canvas.SetLeft(spiritButton, width - 86);
                Canvas.SetTop(spiritButton, 54);
            }
            if (scratchToolbar != null)
            {
                scratchToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(scratchToolbar, width + (ScratchPaneWidth() - scratchToolbar.DesiredSize.Width) / 2);
                Canvas.SetTop(scratchToolbar, (height - scratchToolbar.DesiredSize.Height) / 2);
            }
            if (scratchPane != null)
            {
                scratchPane.Width = ScratchPaneWidth();
                scratchPane.Height = height;
                Canvas.SetLeft(scratchPane, width);
                Canvas.SetTop(scratchPane, 0);
            }
            if (scratchSplitResizeGrip != null)
            {
                scratchSplitResizeGrip.Height = height;
                scratchSplitResizeGrip.Visibility = scratchOpen ? Visibility.Visible : Visibility.Collapsed;
                Canvas.SetLeft(scratchSplitResizeGrip, width - scratchSplitResizeGrip.Width / 2);
                Canvas.SetTop(scratchSplitResizeGrip, 0);
            }
            if (scratchPaneResizeGrip != null)
            {
                scratchPaneResizeGrip.Height = height;
                scratchPaneResizeGrip.Visibility = scratchOpen ? Visibility.Visible : Visibility.Collapsed;
                Canvas.SetLeft(scratchPaneResizeGrip, width + ScratchPaneWidth() - scratchPaneResizeGrip.Width / 2);
                Canvas.SetTop(scratchPaneResizeGrip, 0);
            }
            if (scratchMinimap != null)
            {
                Canvas.SetLeft(scratchMinimap, 16);
                Canvas.SetTop(scratchMinimap, height - 126);
            }
            PositionWindowResizeGrips();
        }

        private void PositionWindowResizeGrips()
        {
            double totalWidth = ActualStageWidth();
            double height = StageHeight();
            const double edge = 8;
            if (windowResizeLeftGrip != null)
            {
                windowResizeLeftGrip.Width = edge;
                windowResizeLeftGrip.Height = height;
                Canvas.SetLeft(windowResizeLeftGrip, 0);
                Canvas.SetTop(windowResizeLeftGrip, 0);
            }
            if (windowResizeRightGrip != null)
            {
                windowResizeRightGrip.Width = edge;
                windowResizeRightGrip.Height = height;
                windowResizeRightGrip.Visibility = scratchOpen ? Visibility.Collapsed : Visibility.Visible;
                Canvas.SetLeft(windowResizeRightGrip, Math.Max(0, totalWidth - edge));
                Canvas.SetTop(windowResizeRightGrip, 0);
            }
            if (windowResizeTopGrip != null)
            {
                windowResizeTopGrip.Width = totalWidth;
                windowResizeTopGrip.Height = edge;
                Canvas.SetLeft(windowResizeTopGrip, 0);
                Canvas.SetTop(windowResizeTopGrip, 0);
            }
            if (windowResizeBottomGrip != null)
            {
                windowResizeBottomGrip.Width = totalWidth;
                windowResizeBottomGrip.Height = edge;
                Canvas.SetLeft(windowResizeBottomGrip, 0);
                Canvas.SetTop(windowResizeBottomGrip, Math.Max(0, height - edge));
            }
        }

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt))
            {
                RevealSpirit();
            }
            if (scratchOpen && e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                PasteScratchImage();
                e.Handled = true;
            }
            if (scratchOpen && e.Key == Key.Delete && !String.IsNullOrEmpty(selectedScratchItemId))
            {
                DeleteScratchItem(selectedScratchItemId);
                e.Handled = true;
            }
            if (scratchOpen && !String.IsNullOrEmpty(drawingScratchItemId))
            {
                if (e.Key == Key.Oem4)
                {
                    AdjustScratchBrushSize(-2);
                    e.Handled = true;
                }
                else if (e.Key == Key.Oem6)
                {
                    AdjustScratchBrushSize(2);
                    e.Handled = true;
                }
            }
            RefreshScratchInkMode();
        }

        private void WindowPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != (ModifierKeys.Control | ModifierKeys.Alt))
            {
                HideSpirit();
            }
            RefreshScratchInkMode();
        }

        private void OnPasteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (!scratchOpen) return;
            PasteScratchImage();
            e.Handled = true;
        }

        private void RevealSpirit()
        {
            if (spiritButton == null || spiritVisible) return;
            spiritVisible = true;
            spiritButton.Visibility = Visibility.Visible;
            spiritButton.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        }

        private void HideSpirit()
        {
            if (spiritButton == null || !spiritVisible) return;
            spiritVisible = false;
            DoubleAnimation fade = new DoubleAnimation(spiritButton.Opacity, 0, TimeSpan.FromMilliseconds(160));
            fade.Completed += delegate { if (!spiritVisible) spiritButton.Visibility = Visibility.Collapsed; };
            spiritButton.BeginAnimation(OpacityProperty, fade);
        }

        private bool IsPointOverSpirit(Point point)
        {
            if (!spiritVisible || spiritButton == null || spiritButton.Visibility != Visibility.Visible) return false;
            double left = Canvas.GetLeft(spiritButton);
            double top = Canvas.GetTop(spiritButton);
            return new Rect(left, top, spiritButton.Width, spiritButton.Height).Contains(point);
        }

        private void ToggleScratchCanvas()
        {
            bool openNext = !scratchOpen;
            if (openNext)
            {
                scratchPaneWidth = MainPaneWidth();
                Width = MainPaneWidth() + ScratchPaneWidth();
                if (WindowStartupLocation == WindowStartupLocation.CenterScreen) WindowStartupLocation = WindowStartupLocation.Manual;
            }
            scratchOpen = openNext;
            selectedId = null;
            selectedScratchItemId = null;
            ExitScratchDrawingMode();
            interaction = null;
            interactionCardId = null;
            if (!scratchOpen) Width = MainPaneWidth();
            if (scratchOpen && windowMode == "normal") Width = MainPaneWidth() + ScratchPaneWidth();
            PositionFloatingUi();
            Render();
            spiritVisible = false;
            if (spiritButton != null) spiritButton.Visibility = Visibility.Collapsed;
            ShowToast(scratchOpen ? "\u4e34\u65f6\u753b\u5e03" : "\u4e3b\u753b\u5e03");
        }

        private void ToggleScratchDrawing()
        {
            if (!String.IsNullOrEmpty(selectedScratchItemId)) EnterScratchDrawingMode(selectedScratchItemId);
        }

        private void AdjustScratchBrushSize(double delta)
        {
            if (scratchBrushSlider == null) return;
            scratchBrushSlider.Value = Clamp(scratchBrushSlider.Value + delta, scratchBrushSlider.Minimum, scratchBrushSlider.Maximum);
            UpdateScratchDrawingAttributes();
        }

        private void UpdateScratchDrawingAttributes()
        {
            double size = scratchBrushSlider == null ? 8 : scratchBrushSlider.Value;
            foreach (ScratchItemControl control in scratchControls.Values)
            {
                control.SetDrawingAttributes(scratchInkColor, size);
            }
        }

        private void EnterScratchDrawingMode(string itemId)
        {
            ScratchItem item = GetScratchItem(itemId);
            if (item == null || item.kind != "image") return;
            drawingScratchItemId = null;
            pendingScratchDrawingItemId = itemId;
            selectedScratchItemId = itemId;
            if (scratchBrushSlider != null) scratchBrushSlider.Value = 8;
            foreach (ScratchItemControl control in scratchControls.Values)
            {
                control.SetSelected(control.Item.id == selectedScratchItemId);
                control.SetDrawing(false, false);
            }
            if (scratchToolbar != null)
            {
                ShowScratchToolbar();
            }
            UpdateScratchDrawingAttributes();
        }

        private void StartPendingScratchDrawing()
        {
            if (String.IsNullOrEmpty(pendingScratchDrawingItemId)) return;
            drawingScratchItemId = pendingScratchDrawingItemId;
            pendingScratchDrawingItemId = null;
            if (scratchToolbar != null) scratchToolbar.Visibility = Visibility.Collapsed;
            UpdateScratchDrawingAttributes();
            RefreshScratchInkMode();
        }

        private void ShowScratchToolbar()
        {
            if (scratchToolbar == null) return;
            scratchToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(scratchToolbar, MainPaneWidth() + (ScratchPaneWidth() - scratchToolbar.DesiredSize.Width) / 2);
            Canvas.SetTop(scratchToolbar, (StageHeight() - scratchToolbar.DesiredSize.Height) / 2);
            scratchToolbar.Visibility = Visibility.Visible;
        }

        private void ExitScratchDrawingMode()
        {
            pendingScratchDrawingItemId = null;
            drawingScratchItemId = null;
            if (scratchToolbar != null) scratchToolbar.Visibility = Visibility.Collapsed;
            foreach (ScratchItemControl control in scratchControls.Values) control.SetDrawing(false, false);
        }

        private bool IsScratchEraserActive()
        {
            return !String.IsNullOrEmpty(drawingScratchItemId) && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        }

        private void RefreshScratchInkMode()
        {
            if (String.IsNullOrEmpty(drawingScratchItemId)) return;
            bool erase = IsScratchEraserActive();
            foreach (ScratchItemControl control in scratchControls.Values)
            {
                control.SetDrawing(control.Item.id == drawingScratchItemId, erase);
            }
        }

        private void Render()
        {
            UpdateWorldTransform();
            world.Children.Clear();
            cardControls.Clear();
            foreach (CardModel card in CurrentCards())
            {
                CardControl control = new CardControl(this, card);
                cardControls[card.id] = control;
                control.SetSelected(card.id == selectedId);
                Canvas.SetLeft(control, card.x);
                Canvas.SetTop(control, card.y);
                SetCardZIndex(card, control, false);
                world.Children.Add(control);
            }
            RenderScratchPane();
            minimap.Visibility = state.minimapOpen ? Visibility.Visible : Visibility.Collapsed;
            mapButton.Background = state.minimapOpen ? Solid(255, 255, 255, 33) : Solid(255, 255, 255, 20);
            archiveButton.Visibility = Visibility.Visible;
            organizeButton.Visibility = Visibility.Visible;
            archiveButton.Background = IsArchiveSide() ? Solid(255, 255, 255, 33) : Solid(255, 255, 255, 20);
            organizeButton.Background = state.organized && state.organizedSide == state.side ? Solid(255, 255, 255, 33) : Solid(255, 255, 255, 20);
            RenderArchiveCount();
            RenderMinimap();
            ScheduleGlassRefresh();
        }

        private void RenderScratchPane()
        {
            if (scratchPane == null || scratchWorld == null) return;
            scratchPane.Visibility = scratchOpen ? Visibility.Visible : Visibility.Collapsed;
            if (!scratchOpen)
            {
                scratchControls.Clear();
                scratchWorld.Children.Clear();
                return;
            }
            UpdateScratchWorldTransform();
            RenderScratchWorld();
            RenderScratchMinimap();
        }

        private void RenderScratchWorld()
        {
            scratchWorld.Children.Clear();
            scratchControls.Clear();
            foreach (ScratchItem item in scratchItems.OrderBy(i => i.createdAt))
            {
                ScratchItemControl control = new ScratchItemControl(this, item);
                scratchControls[item.id] = control;
                control.SetSelected(item.id == selectedScratchItemId);
                control.SetDrawing(item.id == drawingScratchItemId, IsScratchEraserActive());
                control.SetDrawingAttributes(scratchInkColor, scratchBrushSlider == null ? 8 : scratchBrushSlider.Value);
                Canvas.SetLeft(control, item.x);
                Canvas.SetTop(control, item.y);
                Panel.SetZIndex(control, 10 + scratchControls.Count);
                scratchWorld.Children.Add(control);
            }
        }

        private void UpdateWorldTransform()
        {
            ViewportState viewport = CurrentViewport();
            worldScale.ScaleX = viewport.scale;
            worldScale.ScaleY = viewport.scale;
            worldTranslate.X = -viewport.x * viewport.scale;
            worldTranslate.Y = -viewport.y * viewport.scale;
        }

        private void UpdateScratchWorldTransform()
        {
            if (scratchScale == null || scratchTranslate == null) return;
            scratchScale.ScaleX = scratchViewport.scale;
            scratchScale.ScaleY = scratchViewport.scale;
            scratchTranslate.X = -scratchViewport.x * scratchViewport.scale;
            scratchTranslate.Y = -scratchViewport.y * scratchViewport.scale;
        }

        private List<CardModel> CurrentCards()
        {
            bool archive = IsArchiveSide();
            return state.cards.Where(c => c.archived == archive).OrderBy(c => c.createdAt).ToList();
        }

        private List<CardModel> ArchivedCards()
        {
            return state.cards.Where(c => c.archived).ToList();
        }

        private bool IsArchiveSide()
        {
            return state.side == "archive";
        }

        private ViewportState CurrentViewport()
        {
            if (state.viewports == null) state.viewports = DefaultViewports();
            if (!state.viewports.ContainsKey(state.side)) state.viewports[state.side] = new ViewportState { x = 0, y = 0, scale = 1 };
            return state.viewports[state.side];
        }

        private Point ScratchScreenToWorld(Point point)
        {
            return new Point(scratchViewport.x + point.X / scratchViewport.scale, scratchViewport.y + point.Y / scratchViewport.scale);
        }

        private Point ScratchWorldToScreen(Point point)
        {
            return new Point((point.X - scratchViewport.x) * scratchViewport.scale, (point.Y - scratchViewport.y) * scratchViewport.scale);
        }

        private bool IsPointInScratchPane(Point stagePoint)
        {
            return scratchOpen && stagePoint.X >= MainPaneWidth() && stagePoint.X <= MainPaneWidth() + ScratchPaneWidth() && stagePoint.Y >= 0 && stagePoint.Y <= StageHeight();
        }

        private Point StageToScratchPoint(Point stagePoint)
        {
            return new Point(stagePoint.X - MainPaneWidth(), stagePoint.Y);
        }

        private Point ScreenToWorld(Point point)
        {
            ViewportState viewport = CurrentViewport();
            return new Point(viewport.x + point.X / viewport.scale, viewport.y + point.Y / viewport.scale);
        }

        private Point WorldToScreen(Point point)
        {
            ViewportState viewport = CurrentViewport();
            return new Point((point.X - viewport.x) * viewport.scale, (point.Y - viewport.y) * viewport.scale);
        }

        private CardModel GetCard(string id)
        {
            return state.cards.FirstOrDefault(c => c.id == id);
        }

        public void SelectCard(CardModel card)
        {
            selectedId = card.id;
            BringCardToFront(card);
            foreach (CardControl control in cardControls.Values) control.SetSelected(control.Card.id == selectedId);
            HideMenus();
        }

        private void BringCardToFront(CardModel card)
        {
            CardControl control;
            if (cardControls.TryGetValue(card.id, out control))
            {
                SetCardZIndex(card, control, true);
                ScheduleGlassRefresh();
            }
        }

        private void SetCardZIndex(CardModel card, CardControl control, bool front)
        {
            if (zCounter > 90000) zCounter = 1;
            int layer = card.pinned ? 200000 : 10000;
            Panel.SetZIndex(control, layer + (int)++zCounter);
        }

        internal UIElement StageSurface
        {
            get { return stage; }
        }

        internal BitmapSource CaptureBackdropFor(CardControl target, Rect stageRect)
        {
            if (stage == null || renderingBackdrop) return null;
            int stageWidth = Math.Max(1, (int)Math.Ceiling(stage.ActualWidth > 1 ? stage.ActualWidth : Width));
            int stageHeight = Math.Max(1, (int)Math.Ceiling(stage.ActualHeight > 1 ? stage.ActualHeight : Height));
            int x = ClampInt((int)Math.Floor(stageRect.Left), 0, stageWidth - 1);
            int y = ClampInt((int)Math.Floor(stageRect.Top), 0, stageHeight - 1);
            int w = ClampInt((int)Math.Ceiling(stageRect.Width), 1, stageWidth - x);
            int h = ClampInt((int)Math.Ceiling(stageRect.Height), 1, stageHeight - y);
            List<Tuple<UIElement, Visibility>> changed = new List<Tuple<UIElement, Visibility>>();
            int currentZ = Panel.GetZIndex(target);

            Action<UIElement> hide = delegate(UIElement element)
            {
                if (element != null && element.Visibility == Visibility.Visible)
                {
                    changed.Add(new Tuple<UIElement, Visibility>(element, element.Visibility));
                    element.Visibility = Visibility.Hidden;
                }
            };

            foreach (CardControl control in cardControls.Values)
            {
                if (control == target || Panel.GetZIndex(control) >= currentZ) hide(control);
            }
            hide(overlay);
            hide(minimap);
            hide(mapButton);
            hide(archiveButton);
            hide(organizeButton);
            hide(archiveCount);
            hide(toast);
            hide(actionMenu);
            hide(windowChrome);
            hide(spiritButton);
            hide(scratchToolbar);
            hide(scratchPane);

            renderingBackdrop = true;
            try
            {
                RenderTargetBitmap full = new RenderTargetBitmap(stageWidth, stageHeight, 96, 96, PixelFormats.Pbgra32);
                full.Render(stage);
                CroppedBitmap cropped = new CroppedBitmap(full, new Int32Rect(x, y, w, h));
                cropped.Freeze();
                return cropped;
            }
            catch
            {
                return null;
            }
            finally
            {
                foreach (Tuple<UIElement, Visibility> item in changed) item.Item1.Visibility = item.Item2;
                renderingBackdrop = false;
            }
        }

        internal void ScheduleGlassRefresh()
        {
            if (Dispatcher == null) return;
            Dispatcher.BeginInvoke(new Action(RefreshAllCardGlass), DispatcherPriority.Background);
        }

        internal void RefreshAllCardGlass()
        {
            if (renderingBackdrop) return;
            foreach (CardControl control in cardControls.Values.OrderBy(c => Panel.GetZIndex(c)))
            {
                control.RefreshGlass();
            }
        }

        public void BeginCardDrag(CardModel card, MouseButtonEventArgs e)
        {
            SelectCard(card);
            interaction = "drag";
            interactionCardId = card.id;
            pointerStart = e.GetPosition(stage);
            cardStart = new Point(card.x, card.y);
            stage.CaptureMouse();
        }

        public void BeginResize(CardModel card, MouseButtonEventArgs e)
        {
            SelectCard(card);
            interaction = "resize";
            interactionCardId = card.id;
            pointerStart = e.GetPosition(stage);
            cardSizeStart = new Size(card.w, card.h);
            stage.CaptureMouse();
        }

        public void CopyCardContent(CardModel card)
        {
            if (String.IsNullOrWhiteSpace(card.content))
            {
                ShowToast("没有粘贴内容");
                return;
            }
            Clipboard.SetText(card.content);
            ShowToast("复制成功");
        }

        public void BeginScratchItemDrag(ScratchItem item, MouseButtonEventArgs e)
        {
            SelectScratchItem(item);
            interaction = "scratch-drag";
            interactionCardId = item.id;
            pointerStart = e.GetPosition(scratchPane);
            scratchItemStart = new Point(item.x, item.y);
            stage.CaptureMouse();
        }

        public void BeginScratchItemResize(ScratchItem item, MouseButtonEventArgs e, string mode)
        {
            SelectScratchItem(item);
            interaction = "scratch-resize";
            interactionCardId = item.id;
            scratchResizeMode = mode;
            pointerStart = e.GetPosition(scratchPane);
            scratchItemSizeStart = new Size(item.w, item.h);
            scratchItemStart = new Point(item.x, item.y);
            stage.CaptureMouse();
        }

        public void CopyScratchItem(ScratchItem item)
        {
            if (item == null || String.IsNullOrWhiteSpace(item.content))
            {
                ShowToast("\u6ca1\u6709\u7c98\u8d34\u5185\u5bb9");
                return;
            }
            Clipboard.SetText(item.content);
            ShowToast("\u590d\u5236\u6210\u529f");
        }

        public void ShowScratchItemMenu(ScratchItem item, Point screenPoint)
        {
            if (!String.IsNullOrEmpty(drawingScratchItemId) || !String.IsNullOrEmpty(pendingScratchDrawingItemId))
            {
                if (item != null && item.kind == "image" && item.id == drawingScratchItemId)
                {
                    selectedScratchItemId = item.id;
                    ShowScratchToolbar();
                    return;
                }
                ExitScratchDrawingMode();
                return;
            }
            SelectScratchItem(item);
            List<MenuAction> actions = new List<MenuAction>();
            if (item.kind == "image") actions.Add(new MenuAction("\u6d82\u9e26", delegate { EnterScratchDrawingMode(item.id); }));
            if (item.kind == "mirror") actions.Add(new MenuAction("\u590d\u5236", delegate { CopyScratchItem(item); }));
            actions.Add(new MenuAction("\u53e6\u5b58\u4e3a", delegate { SaveScratchItem(item); }));
            actions.Add(new MenuAction("\u5220\u9664", delegate { DeleteScratchItem(item.id); }));
            ShowActionMenu(PointFromScreen(screenPoint), actions);
        }

        public void SelectScratchItem(ScratchItem item)
        {
            selectedScratchItemId = item == null ? null : item.id;
            foreach (ScratchItemControl control in scratchControls.Values)
            {
                control.SetSelected(control.Item.id == selectedScratchItemId);
            }
        }

        public void BeginTitleEdit(CardControl control)
        {
            if (IsArchiveSide()) return;
            control.BeginTitleEdit();
        }

        public void ShowCardMenu(CardModel card, Point screenPoint)
        {
            SelectCard(card);
            List<MenuAction> actions = new List<MenuAction>();
            if (IsArchiveSide())
            {
                actions.Add(new MenuAction("恢复", delegate { RestoreCard(card); }));
                actions.Add(new MenuAction("删除", delegate { Confirm("删除这张卡片？", "删除后将从归档画布中移除。", "删除", delegate { DeleteArchivedCard(card); }); }));
            }
            else
            {
                actions.Add(new MenuAction("标签", delegate { ShowTagDialog(card); }));
                actions.Add(new MenuAction("粘贴内容", delegate { ShowPasteDialog(card); }));
                actions.Add(new MenuAction(card.pinned ? "取消置顶" : "置顶", delegate { TogglePinned(card); }));
                actions.Add(new MenuAction("归档", delegate { Confirm("归档这张卡片？", "它会进入背面的无限画布，可以随时恢复。", "归档", delegate { ArchiveCard(card); }); }));
            }
            ShowActionMenu(PointFromScreen(screenPoint), actions);
        }

        private void ShowActionMenu(Point stagePoint, List<MenuAction> actions)
        {
            actionMenuItems.Children.Clear();
            foreach (MenuAction action in actions)
            {
                Button button = new Button();
                button.Content = action.Text;
                button.Height = 34;
                button.MinWidth = 106;
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Padding = new Thickness(10, 0, 12, 0);
                button.Margin = new Thickness(0);
                button.Foreground = Solid(248, 248, 244, 214);
                button.Background = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.Template = FlatMenuButtonTemplate();
                button.Click += delegate
                {
                    HideMenus();
                    action.Execute();
                };
                actionMenuItems.Children.Add(button);
            }
            actionMenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double left = Clamp(stagePoint.X, 8, ActualStageWidth() - Math.Max(132, actionMenu.DesiredSize.Width) - 8);
            double top = Clamp(stagePoint.Y, 8, StageHeight() - Math.Max(48, actionMenu.DesiredSize.Height) - 8);
            Canvas.SetLeft(actionMenu, left);
            Canvas.SetTop(actionMenu, top);
            actionMenu.Visibility = Visibility.Visible;
            Panel.SetZIndex(actionMenu, 90);
        }

        private void HideMenus()
        {
            if (actionMenu != null) actionMenu.Visibility = Visibility.Collapsed;
        }

        private ControlTemplate FlatMenuButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(0));
            border.AppendChild(presenter);
            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        private void ShowTagDialog(CardModel card)
        {
            TagDialog dialog = new TagDialog(this, card.tag, Tags);
            if (dialog.ShowDialog() == true)
            {
                card.tag = dialog.SelectedTag;
                ClearOrganizeState();
                SaveStateSoon();
                Render();
            }
        }

        private void ShowPasteDialog(CardModel card)
        {
            PasteDialog dialog = new PasteDialog(this, card.content);
            if (dialog.ShowDialog() == true)
            {
                card.content = dialog.Value;
                SaveStateSoon();
                ShowToast("已保存");
            }
        }

        private void TogglePinned(CardModel card)
        {
            card.pinned = !card.pinned;
            selectedId = card.id;
            ClearOrganizeState();
            SaveStateSoon();
            Render();
            ShowToast(card.pinned ? "已置顶" : "已取消置顶");
        }

        private void Confirm(string title, string body, string okText, Action action)
        {
            ConfirmDialog dialog = new ConfirmDialog(this, title, body, okText);
            if (dialog.ShowDialog() == true) action();
        }

        private void ArchiveCard(CardModel card)
        {
            int existing = ArchivedCards().Count;
            ViewportState archiveViewport = state.viewports["archive"];
            card.frontSnapshot = new CardSnapshot { x = card.x, y = card.y, w = card.w, h = card.h };
            card.archived = true;
            card.archivedAt = NowMs();
            card.x = archiveViewport.x + 48 + (existing % 5) * 18;
            card.y = archiveViewport.y + 96 + (existing % 5) * 18;
            if (selectedId == card.id) selectedId = null;
            ClearOrganizeState();
            SaveStateSoon();
            Render();
            ShowToast("已归档");
        }

        private void RestoreCard(CardModel card)
        {
            CardSnapshot snapshot = card.frontSnapshot;
            card.archived = false;
            card.x = snapshot != null ? snapshot.x : state.viewports["front"].x + 48;
            card.y = snapshot != null ? snapshot.y : state.viewports["front"].y + 96;
            card.w = snapshot != null ? snapshot.w : card.w;
            card.h = snapshot != null ? snapshot.h : card.h;
            card.frontSnapshot = null;
            state.side = "front";
            selectedId = card.id;
            state.viewports["front"].x = card.x - 48;
            state.viewports["front"].y = card.y - 96;
            ClearOrganizeState();
            SaveStateSoon();
            Render();
            ShowToast("已恢复");
        }

        private void DeleteArchivedCard(CardModel card)
        {
            state.cards.Remove(card);
            SaveStateSoon();
            Render();
            ShowToast("已删除");
        }

        private void ClearArchive()
        {
            state.cards = state.cards.Where(c => !c.archived).ToList();
            SaveStateSoon();
            Render();
            ShowToast("已清空");
        }

        private void BeginSelection(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.OriginalSource != stage && e.OriginalSource != overlay && e.OriginalSource != world) return;
            if (IsPointInScratchPane(e.GetPosition(stage))) return;
            if (IsArchiveSide()) return;
            HideMenus();
            interaction = "selection";
            selectionStart = e.GetPosition(stage);
            selectionBox.Visibility = Visibility.Visible;
            Canvas.SetLeft(selectionBox, selectionStart.X);
            Canvas.SetTop(selectionBox, selectionStart.Y);
            selectionBox.Width = 0;
            selectionBox.Height = 0;
            stage.CaptureMouse();
            e.Handled = true;
        }

        private void StageMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && actionMenu != null && actionMenu.Visibility == Visibility.Visible && !actionMenu.IsMouseOver)
            {
                HideMenus();
            }
            Point stagePoint = e.GetPosition(stage);
            if (e.ChangedButton == MouseButton.Left && IsPointInScratchPane(stagePoint))
            {
                if (e.OriginalSource == scratchPane || e.OriginalSource == scratchWorld || e.OriginalSource == scratchOverlay)
                {
                    SelectScratchItem(null);
                    ExitScratchDrawingMode();
                }
            }
            if (e.ChangedButton == MouseButton.Middle)
            {
                HideMenus();
                if (IsPointInScratchPane(stagePoint))
                {
                    interaction = "scratch-pan";
                    pointerStart = StageToScratchPoint(stagePoint);
                    viewportStart = new ViewportState { x = scratchViewport.x, y = scratchViewport.y, scale = scratchViewport.scale };
                }
                else
                {
                    interaction = "pan";
                    pointerStart = stagePoint;
                    ViewportState current = CurrentViewport();
                    viewportStart = new ViewportState { x = current.x, y = current.y, scale = current.scale };
                }
                stage.Cursor = Cursors.Hand;
                stage.CaptureMouse();
                e.Handled = true;
            }
        }

        private void StageMouseMove(object sender, MouseEventArgs e)
        {
            if (interaction == null) return;
            Point point = e.GetPosition(stage);
            ViewportState viewport = CurrentViewport();
            if (interaction == "selection")
            {
                double left = Math.Min(selectionStart.X, point.X);
                double top = Math.Min(selectionStart.Y, point.Y);
                selectionBox.Width = Math.Abs(point.X - selectionStart.X);
                selectionBox.Height = Math.Abs(point.Y - selectionStart.Y);
                Canvas.SetLeft(selectionBox, left);
                Canvas.SetTop(selectionBox, top);
            }
            else if (interaction == "pan")
            {
                viewport.x = viewportStart.x - (point.X - pointerStart.X) / viewport.scale;
                viewport.y = viewportStart.y - (point.Y - pointerStart.Y) / viewport.scale;
                UpdateWorldTransform();
                RenderMinimap();
            }
            else if (interaction == "scratch-pan")
            {
                Point scratchPoint = StageToScratchPoint(point);
                scratchViewport.x = viewportStart.x - (scratchPoint.X - pointerStart.X) / scratchViewport.scale;
                scratchViewport.y = viewportStart.y - (scratchPoint.Y - pointerStart.Y) / scratchViewport.scale;
                UpdateScratchWorldTransform();
                RenderScratchMinimap();
            }
            else if (interaction == "scratch-pane-resize")
            {
                scratchPaneWidth = Clamp(scratchPaneWidthStart + point.X - pointerStart.X, 320, 1800);
                Width = MainPaneWidth() + ScratchPaneWidth();
                PositionFloatingUi();
                RenderScratchMinimap();
            }
            else if (interaction == "pane-split-resize")
            {
                double total = mainPaneWidthStart + scratchPaneWidthStart;
                mainPaneWidth = Clamp(mainPaneWidthStart + point.X - pointerStart.X, 360, total - 320);
                scratchPaneWidth = Math.Max(320, total - mainPaneWidth);
                PositionFloatingUi();
                RenderMinimap();
                RenderScratchMinimap();
            }
            else if (interaction == "window-resize")
            {
                ResizeWindowFromPointer(point);
            }
            else if (interaction == "drag")
            {
                CardModel card = GetCard(interactionCardId);
                if (card == null) return;
                card.x = cardStart.X + (point.X - pointerStart.X) / viewport.scale;
                card.y = cardStart.Y + (point.Y - pointerStart.Y) / viewport.scale;
                CardControl control;
                if (cardControls.TryGetValue(card.id, out control))
                {
                    Canvas.SetLeft(control, card.x);
                    Canvas.SetTop(control, card.y);
                    control.RefreshGlass();
                }
                ClearOrganizeState();
                RenderMinimap();
            }
            else if (interaction == "resize")
            {
                CardModel card = GetCard(interactionCardId);
                if (card == null) return;
                card.w = Clamp(cardSizeStart.Width + (point.X - pointerStart.X) / viewport.scale, 104, 520);
                card.h = Clamp(cardSizeStart.Height + (point.Y - pointerStart.Y) / viewport.scale, 64, 420);
                CardControl control;
                if (cardControls.TryGetValue(card.id, out control))
                {
                    control.UpdateSize();
                    control.RefreshGlass();
                }
                ClearOrganizeState();
                RenderMinimap();
            }
            else if (interaction == "scratch-drag")
            {
                ScratchItem item = GetScratchItem(interactionCardId);
                if (item == null) return;
                Point scratchPoint = StageToScratchPoint(point);
                item.x = scratchItemStart.X + (scratchPoint.X - pointerStart.X) / scratchViewport.scale;
                item.y = scratchItemStart.Y + (scratchPoint.Y - pointerStart.Y) / scratchViewport.scale;
                ScratchItemControl control;
                if (scratchControls.TryGetValue(item.id, out control))
                {
                    Canvas.SetLeft(control, item.x);
                    Canvas.SetTop(control, item.y);
                }
                RenderScratchMinimap();
            }
            else if (interaction == "scratch-resize")
            {
                ScratchItem item = GetScratchItem(interactionCardId);
                if (item == null) return;
                Point scratchPoint = StageToScratchPoint(point);
                double dx = (scratchPoint.X - pointerStart.X) / scratchViewport.scale;
                double dy = (scratchPoint.Y - pointerStart.Y) / scratchViewport.scale;
                if (item.kind == "image")
                {
                    double aspect = item.aspect <= 0 ? 1 : item.aspect;
                    double signedDx = scratchResizeMode != null && scratchResizeMode.Contains("w") ? -dx : dx;
                    double signedDy = scratchResizeMode != null && scratchResizeMode.Contains("n") ? -dy : dy;
                    double nextW = Math.Max(scratchItemSizeStart.Width + signedDx, (scratchItemSizeStart.Height + signedDy) * aspect);
                    item.w = Clamp(nextW, 48, 1200);
                    item.h = item.w / aspect;
                    if (scratchResizeMode != null && scratchResizeMode.Contains("w")) item.x = scratchItemStart.X + (scratchItemSizeStart.Width - item.w);
                    if (scratchResizeMode != null && scratchResizeMode.Contains("n")) item.y = scratchItemStart.Y + (scratchItemSizeStart.Height - item.h);
                }
                else
                {
                    item.w = Clamp(scratchItemSizeStart.Width + dx, 104, 520);
                    item.h = Clamp(scratchItemSizeStart.Height + dy, 64, 420);
                }
                ScratchItemControl control;
                if (scratchControls.TryGetValue(item.id, out control))
                {
                    Canvas.SetLeft(control, item.x);
                    Canvas.SetTop(control, item.y);
                    control.UpdateSize();
                }
                RenderScratchMinimap();
            }
        }

        private void StageMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (interaction != "selection") return;
            FinishSelection(e.GetPosition(stage));
            e.Handled = true;
        }

        private void StageMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (interaction == null) return;
            if (interaction == "selection")
            {
                FinishSelection(e.GetPosition(stage));
            }
            else if (interaction == "drag" && !scratchOpen)
            {
                TryDropCardToScratch(e.GetPosition(stage));
                SaveStateSoon();
            }
            else if (interaction == "drag")
            {
                TryDropCardToScratch(e.GetPosition(stage));
                SaveStateSoon();
            }
            else if (interaction == "scratch-pane-resize")
            {
                PositionFloatingUi();
                RenderScratchMinimap();
                SaveStateSoon();
            }
            else if (interaction == "pane-split-resize" || interaction == "window-resize")
            {
                PositionFloatingUi();
                RenderMinimap();
                RenderScratchMinimap();
                SaveStateSoon();
            }
            else
            {
                if (!scratchOpen) SaveStateSoon();
            }
            interaction = null;
            interactionCardId = null;
            scratchResizeMode = null;
            windowResizeMode = null;
            selectionBox.Visibility = Visibility.Collapsed;
            stage.ReleaseMouseCapture();
            stage.Cursor = Cursors.Arrow;
            ScheduleGlassRefresh();
        }

        private void FinishSelection(Point end)
        {
            Rect screenRect = new Rect(selectionStart, end);
            screenRect = Normalize(screenRect);
            selectionBox.Visibility = Visibility.Collapsed;
            if (screenRect.Width >= 54 && screenRect.Height >= 44)
            {
                Point worldTopLeft = ScreenToWorld(new Point(screenRect.Left, screenRect.Top));
                ViewportState viewport = CurrentViewport();
                CreateCard(worldTopLeft.X, worldTopLeft.Y, screenRect.Width / viewport.scale, screenRect.Height / viewport.scale, "", "");
            }
            interaction = null;
            stage.ReleaseMouseCapture();
        }

        private Rect Normalize(Rect rect)
        {
            double left = Math.Min(rect.Left, rect.Right);
            double top = Math.Min(rect.Top, rect.Bottom);
            return new Rect(left, top, Math.Abs(rect.Width), Math.Abs(rect.Height));
        }

        private CardModel CreateCard(double x, double y, double w, double h, string title, string content)
        {
            CardModel card = new CardModel();
            card.id = Guid.NewGuid().ToString("N");
            card.x = x;
            card.y = y;
            card.w = Clamp(w, 104, 520);
            card.h = Clamp(h, 64, 420);
            card.title = title ?? "";
            card.content = content ?? "";
            card.tag = "gray";
            card.archived = false;
            card.createdAt = NowMs();
            state.cards.Add(card);
            ClearOrganizeState();
            SaveStateSoon();
            Render();
            return card;
        }

        private ScratchItem GetScratchItem(string id)
        {
            return scratchItems.FirstOrDefault(i => i.id == id);
        }

        private void TryDropCardToScratch(Point stagePoint)
        {
            CardModel card = GetCard(interactionCardId);
            if (card == null) return;
            bool dropOnScratch = IsPointInScratchPane(stagePoint);
            bool dropOnSpirit = IsPointOverSpirit(stagePoint);
            if (!dropOnScratch && !dropOnSpirit) return;
            card.x = cardStart.X;
            card.y = cardStart.Y;
            Point scratchPoint = dropOnScratch ? StageToScratchPoint(stagePoint) : new Point(54, 92);
            AddScratchMirror(card, scratchPoint);
            if (!scratchOpen) ToggleScratchCanvas();
            else Render();
        }

        private void AddScratchMirror(CardModel card, Point scratchPoint)
        {
            ScratchItem item = new ScratchItem();
            item.id = Guid.NewGuid().ToString("N");
            item.kind = "mirror";
            Point worldPoint = ScratchScreenToWorld(scratchPoint);
            item.x = worldPoint.X;
            item.y = worldPoint.Y;
            item.w = card.w;
            item.h = card.h;
            item.aspect = card.h <= 0 ? 1 : card.w / card.h;
            item.title = card.title ?? "";
            item.content = card.content ?? "";
            item.tag = String.IsNullOrEmpty(card.tag) ? "gray" : card.tag;
            item.createdAt = NowMs();
            scratchItems.Add(item);
            ShowToast("\u5df2\u52a0\u5165\u4e34\u65f6\u753b\u5e03");
        }

        private void AddScratchImage(BitmapSource source, Point screenPoint)
        {
            if (source == null) return;
            BitmapSource clean = NormalizeBitmap(source);
            Point worldPoint = ScratchScreenToWorld(screenPoint);
            double pixelWidth = Math.Max(1, clean.PixelWidth);
            double pixelHeight = Math.Max(1, clean.PixelHeight);
            double aspect = pixelWidth / pixelHeight;
            double maxW = Math.Min(360, ScratchPaneWidth() / Math.Max(0.35, scratchViewport.scale) * 0.68);
            double maxH = Math.Min(360, StageHeight() / Math.Max(0.35, scratchViewport.scale) * 0.42);
            double scale = Math.Min(1, Math.Min(maxW / pixelWidth, maxH / pixelHeight));
            ScratchItem item = new ScratchItem();
            item.id = Guid.NewGuid().ToString("N");
            item.kind = "image";
            item.x = worldPoint.X;
            item.y = worldPoint.Y;
            item.w = Math.Max(64, pixelWidth * scale);
            item.h = Math.Max(48, pixelHeight * scale);
            item.aspect = aspect;
            item.createdAt = NowMs();
            item.image = clean;
            item.strokes = new StrokeCollection();
            scratchItems.Add(item);
            selectedScratchItemId = item.id;
            Render();
            ShowToast("\u5df2\u6dfb\u52a0\u56fe\u7247");
        }

        private BitmapSource LoadBitmapFromFile(string path)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private BitmapSource NormalizeBitmap(BitmapSource source)
        {
            if (source == null) return null;
            FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[Math.Max(1, stride * height)];
            converted.CopyPixels(pixels, stride, 0);
            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                byte alpha = pixels[i + 3];
                if (alpha > 0 && alpha < 255)
                {
                    pixels[i] = (byte)Math.Min(255, pixels[i] * 255 / alpha);
                    pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / alpha);
                    pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / alpha);
                }
                pixels[i + 3] = 255;
            }
            BitmapSource opaque = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            opaque.Freeze();
            return opaque;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource GetClipboardBitmap()
        {
            try
            {
                if (Clipboard.ContainsData(DataFormats.FileDrop))
                {
                    string[] paths = Clipboard.GetData(DataFormats.FileDrop) as string[];
                    if (paths != null)
                    {
                        foreach (string path in paths)
                        {
                            if (IsImageFile(path)) return LoadBitmapFromFile(path);
                        }
                    }
                }
            }
            catch { }
            try
            {
                BitmapSource dib = GetClipboardDibBitmap();
                if (dib != null) return dib;
            }
            catch { }
            try
            {
                BitmapSource png = GetClipboardPngBitmap();
                if (png != null) return png;
            }
            catch { }
            try
            {
                System.Drawing.Image formsImage = Forms.Clipboard.GetImage();
                if (formsImage != null)
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        formsImage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        return LoadBitmapFromStream(stream);
                    }
                }
            }
            catch { }
            try
            {
                if (Clipboard.ContainsData(DataFormats.Bitmap))
                {
                    BitmapSource source = Clipboard.GetData(DataFormats.Bitmap) as BitmapSource;
                    if (source != null) return source;
                    System.Drawing.Bitmap bitmap = Clipboard.GetData(DataFormats.Bitmap) as System.Drawing.Bitmap;
                    if (bitmap != null)
                    {
                        using (MemoryStream stream = new MemoryStream())
                        {
                            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            return LoadBitmapFromStream(stream);
                        }
                    }
                }
            }
            catch { }
            try
            {
                return Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
            }
            catch { return null; }
        }

        private BitmapSource GetClipboardPngBitmap()
        {
            if (!Clipboard.ContainsData("PNG")) return null;
            object data = Clipboard.GetData("PNG");
            Stream stream = data as Stream;
            if (stream != null) return LoadBitmapFromStream(stream);
            byte[] bytes = data as byte[];
            if (bytes != null)
            {
                using (MemoryStream memory = new MemoryStream(bytes))
                {
                    return LoadBitmapFromStream(memory);
                }
            }
            return null;
        }

        private BitmapSource LoadBitmapFromStream(Stream input)
        {
            if (input == null) return null;
            if (input.CanSeek) input.Position = 0;
            MemoryStream copy = new MemoryStream();
            input.CopyTo(copy);
            copy.Position = 0;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = copy;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private BitmapSource GetClipboardDibBitmap()
        {
            object data = null;
            if (Clipboard.ContainsData(DataFormats.Dib)) data = Clipboard.GetData(DataFormats.Dib);
            else if (Clipboard.ContainsData("DeviceIndependentBitmap")) data = Clipboard.GetData("DeviceIndependentBitmap");
            if (data == null) return null;
            MemoryStream stream = data as MemoryStream;
            if (stream == null)
            {
                Stream rawStream = data as Stream;
                if (rawStream != null)
                {
                    stream = new MemoryStream();
                    if (rawStream.CanSeek) rawStream.Position = 0;
                    rawStream.CopyTo(stream);
                }
            }
            if (stream == null) return null;
            byte[] dib = stream.ToArray();
            if (dib.Length < 40) return null;
            int headerSize = BitConverter.ToInt32(dib, 0);
            int width = BitConverter.ToInt32(dib, 4);
            int rawHeight = BitConverter.ToInt32(dib, 8);
            short bitCount = BitConverter.ToInt16(dib, 14);
            int compression = BitConverter.ToInt32(dib, 16);
            int colorsUsed = headerSize >= 36 ? BitConverter.ToInt32(dib, 32) : 0;
            if (width <= 0 || rawHeight == 0) return null;
            int height = Math.Abs(rawHeight);
            bool topDown = rawHeight < 0;
            int colorTable = bitCount <= 8 ? (colorsUsed > 0 ? colorsUsed : (1 << bitCount)) * 4 : 0;
            int maskBytes = (compression == 3 && headerSize == 40) ? 12 : 0;
            int offset = headerSize + maskBytes + colorTable;
            if (offset >= dib.Length) return null;
            byte[] pixels = new byte[width * height * 4];
            if (bitCount == 32)
            {
                int sourceStride = width * 4;
                for (int y = 0; y < height; y++)
                {
                    int sourceY = topDown ? y : height - 1 - y;
                    int sourceIndex = offset + sourceY * sourceStride;
                    int targetIndex = y * width * 4;
                    if (sourceIndex + sourceStride > dib.Length) return null;
                    for (int x = 0; x < width; x++)
                    {
                        int si = sourceIndex + x * 4;
                        int ti = targetIndex + x * 4;
                        pixels[ti] = dib[si];
                        pixels[ti + 1] = dib[si + 1];
                        pixels[ti + 2] = dib[si + 2];
                        pixels[ti + 3] = 255;
                    }
                }
            }
            else if (bitCount == 24)
            {
                int sourceStride = ((width * 3 + 3) / 4) * 4;
                for (int y = 0; y < height; y++)
                {
                    int sourceY = topDown ? y : height - 1 - y;
                    int sourceIndex = offset + sourceY * sourceStride;
                    int targetIndex = y * width * 4;
                    if (sourceIndex + width * 3 > dib.Length) return null;
                    for (int x = 0; x < width; x++)
                    {
                        int si = sourceIndex + x * 3;
                        int ti = targetIndex + x * 4;
                        pixels[ti] = dib[si];
                        pixels[ti + 1] = dib[si + 1];
                        pixels[ti + 2] = dib[si + 2];
                        pixels[ti + 3] = 255;
                    }
                }
            }
            else return null;
            BitmapSource bitmapSource = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            bitmapSource.Freeze();
            return bitmapSource;
        }

        private bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff";
        }

        private void ImportScratchFiles(string[] paths, Point screenPoint)
        {
            double offset = 0;
            int imported = 0;
            foreach (string path in paths)
            {
                if (!IsImageFile(path)) continue;
                try
                {
                    AddScratchImage(LoadBitmapFromFile(path), new Point(screenPoint.X + offset, screenPoint.Y + offset));
                    offset += 24;
                    imported++;
                }
                catch { }
            }
            if (imported == 0) ShowToast("\u53ea\u652f\u6301\u56fe\u7247");
        }

        private void PasteScratchImage()
        {
            if (!scratchOpen) return;
            BitmapSource image = GetClipboardBitmap();
            if (image != null) AddScratchImage(image, new Point(ScratchPaneWidth() * 0.5, StageHeight() * 0.42));
        }

        private void SaveScratchItem(ScratchItem item)
        {
            if (item == null) return;
            SaveFileDialog dialog = new SaveFileDialog();
            if (item.kind == "image")
            {
                dialog.Filter = "PNG Image|*.png";
                dialog.FileName = "linbo-image.png";
                if (dialog.ShowDialog(this) == true)
                {
                    BitmapSource output = item.image;
                    ScratchItemControl control;
                    if (scratchControls.TryGetValue(item.id, out control))
                    {
                        BitmapSource rendered = control.RenderContentBitmap();
                        if (rendered != null) output = rendered;
                    }
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(output));
                    using (FileStream stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(stream);
                    }
                    ShowToast("\u5df2\u53e6\u5b58");
                }
            }
            else
            {
                dialog.Filter = "Text File|*.txt";
                dialog.FileName = SanitizeFileName(String.IsNullOrWhiteSpace(item.title) ? "linbo-card" : item.title) + ".txt";
                if (dialog.ShowDialog(this) == true)
                {
                    File.WriteAllText(dialog.FileName, item.content ?? item.title ?? "", Encoding.UTF8);
                    ShowToast("\u5df2\u53e6\u5b58");
                }
            }
        }

        private void DeleteScratchItem(string id)
        {
            ScratchItem item = GetScratchItem(id);
            if (item == null) return;
            scratchItems.Remove(item);
            if (selectedScratchItemId == id) selectedScratchItemId = null;
            if (drawingScratchItemId == id || pendingScratchDrawingItemId == id) ExitScratchDrawingMode();
            Render();
        }

        private string SanitizeFileName(string name)
        {
            string value = String.IsNullOrWhiteSpace(name) ? "linbo" : Regex.Replace(name.Trim(), @"\s+", " ");
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            if (value.Length > 80) value = value.Substring(0, 80).Trim();
            return String.IsNullOrWhiteSpace(value) ? "linbo" : value;
        }

        private void StageMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point stagePoint = e.GetPosition(stage);
            if (IsPointInScratchPane(stagePoint))
            {
                Point scratchWheelPoint = StageToScratchPoint(stagePoint);
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    Point before = ScratchScreenToWorld(scratchWheelPoint);
                    double nextScale = Clamp(scratchViewport.scale * Math.Exp(e.Delta * 0.0015), 0.35, 2.4);
                    scratchViewport.scale = nextScale;
                    scratchViewport.x = before.X - scratchWheelPoint.X / nextScale;
                    scratchViewport.y = before.Y - scratchWheelPoint.Y / nextScale;
                }
                else
                {
                    scratchViewport.y -= e.Delta / scratchViewport.scale;
                }
                UpdateScratchWorldTransform();
                RenderScratchMinimap();
                e.Handled = true;
                return;
            }
            ViewportState viewport = CurrentViewport();
            Point point = stagePoint;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Point before = ScreenToWorld(point);
                double nextScale = Clamp(viewport.scale * Math.Exp(e.Delta * 0.0015), 0.35, 2.4);
                viewport.scale = nextScale;
                viewport.x = before.X - point.X / nextScale;
                viewport.y = before.Y - point.Y / nextScale;
            }
            else
            {
                viewport.y -= e.Delta / viewport.scale;
            }
            UpdateWorldTransform();
            RenderMinimap();
            SaveStateSoon();
            e.Handled = true;
        }

        private void StageRightClick(object sender, MouseButtonEventArgs e)
        {
            Point stagePoint = e.GetPosition(stage);
            if (scratchOpen && IsPointInScratchPane(stagePoint))
            {
                if (!String.IsNullOrEmpty(drawingScratchItemId) || !String.IsNullOrEmpty(pendingScratchDrawingItemId))
                {
                    ExitScratchDrawingMode();
                    e.Handled = true;
                    return;
                }
                SelectScratchItem(null);
                HideMenus();
                e.Handled = true;
                return;
            }
            if (!IsArchiveSide()) return;
            if (e.OriginalSource is CardControl) return;
            List<MenuAction> actions = new List<MenuAction>();
            actions.Add(new MenuAction("清空归档", delegate {
                if (ArchivedCards().Count == 0) ShowToast("归档为空");
                else Confirm("清空归档？", "归档画布里的所有卡片都会被删除。", "清空", delegate { ClearArchive(); });
            }));
            ShowActionMenu(e.GetPosition(stage), actions);
            e.Handled = true;
        }

        private void OrganizeCards()
        {
            List<CardModel> cards = CurrentCards()
                .OrderByDescending(c => c.pinned)
                .ThenBy(c => TagRank(c.tag))
                .ThenBy(c => c.createdAt)
                .ToList();
            if (cards.Count == 0) return;
            if (state.organized && state.organizedSide == state.side && state.organizedSnapshot != null)
            {
                foreach (CardModel card in cards)
                {
                    CardSnapshot snap;
                    if (state.organizedSnapshot.TryGetValue(card.id, out snap))
                    {
                        card.x = snap.x;
                        card.y = snap.y;
                        card.w = snap.w;
                        card.h = snap.h;
                    }
                }
                state.organized = false;
                state.organizedSnapshot = null;
                SaveStateSoon();
                Render();
                return;
            }

            ViewportState viewport = CurrentViewport();
            double visibleWidth = StageWidth() / viewport.scale;
            int columns = 2;
            double gap = 14;
            double side = 24;
            double layoutWidth = Math.Max(320, visibleWidth);
            double colWidth = (layoutWidth - side * 2 - gap * (columns - 1)) / columns;
            double startX = viewport.x + side;
            double startY = viewport.y + 76;
            double[] heights = new double[columns];
            for (int i = 0; i < columns; i++) heights[i] = startY;
            state.organizedSnapshot = new Dictionary<string, CardSnapshot>();
            state.organizedSide = state.side;
            foreach (CardModel card in cards)
            {
                state.organizedSnapshot[card.id] = new CardSnapshot { x = card.x, y = card.y, w = card.w, h = card.h };
                int col = 0;
                for (int i = 1; i < columns; i++) if (heights[i] < heights[col]) col = i;
                card.x = startX + col * (colWidth + gap);
                card.y = heights[col];
                card.w = Math.Max(116, Math.Min(card.w, colWidth));
                card.h = Math.Max(64, card.h);
                heights[col] += card.h + gap;
            }
            state.organized = true;
            SaveStateSoon();
            Render();
        }

        private int TagRank(string tag)
        {
            if (tag == "red") return 0;
            if (tag == "gold") return 1;
            if (tag == "blue") return 2;
            return 3;
        }

        private void FlipSide(string side)
        {
            if (state.side == side) return;
            HideMenus();
            double oldScale = Math.Max(0.02, CurrentViewport().scale);
            DoubleAnimation fold = new DoubleAnimation(oldScale, 0.02, TimeSpan.FromMilliseconds(170));
            fold.Completed += delegate
            {
                state.side = side;
                selectedId = null;
                state.organized = false;
                state.organizedSnapshot = null;
                Render();
                double nextScale = Math.Max(0.02, CurrentViewport().scale);
                worldScale.ScaleX = 0.02;
                DoubleAnimation unfold = new DoubleAnimation(0.02, nextScale, TimeSpan.FromMilliseconds(190));
                worldScale.BeginAnimation(ScaleTransform.ScaleXProperty, unfold);
                SaveStateSoon();
            };
            worldScale.BeginAnimation(ScaleTransform.ScaleXProperty, fold);
        }

        private void RenderArchiveCount()
        {
            int count = ArchivedCards().Count;
            archiveCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            archiveCountText.Text = count.ToString();
        }

        private void RenderMinimap()
        {
            if (minimap == null || minimap.Visibility != Visibility.Visible) return;
            minimapWorld.Children.Clear();
            Rect bounds = GetWorldBounds();
            double inset = 10;
            double scale = Math.Min((minimap.Width - inset * 2) / Math.Max(1, bounds.Width), (minimap.Height - inset * 2) / Math.Max(1, bounds.Height));
            double offsetX = (minimap.Width - bounds.Width * scale) / 2;
            double offsetY = (minimap.Height - bounds.Height * scale) / 2;
            minimapMetrics = new MinimapMetrics { Bounds = bounds, Scale = scale, OffsetX = offsetX, OffsetY = offsetY };
            foreach (CardModel card in CurrentCards())
            {
                Rectangle dot = new Rectangle();
                dot.RadiusX = 2;
                dot.RadiusY = 2;
                dot.Width = Math.Max(4, card.w * scale);
                dot.Height = Math.Max(3, card.h * scale);
                dot.Fill = new SolidColorBrush(Mix(TagColor(card.tag), Colors.White, 0.74, 220));
                Canvas.SetLeft(dot, offsetX + (card.x - bounds.Left) * scale);
                Canvas.SetTop(dot, offsetY + (card.y - bounds.Top) * scale);
                minimapWorld.Children.Add(dot);
            }
            Rect vp = GetMinimapViewportBox();
            minimapViewport.Width = vp.Width;
            minimapViewport.Height = vp.Height;
            Canvas.SetLeft(minimapViewport, vp.Left);
            Canvas.SetTop(minimapViewport, vp.Top);
        }

        private Rect GetWorldBounds()
        {
            ViewportState viewport = CurrentViewport();
            double minX = viewport.x;
            double minY = viewport.y;
            double maxX = viewport.x + StageWidth() / viewport.scale;
            double maxY = viewport.y + StageHeight() / viewport.scale;
            foreach (CardModel card in CurrentCards())
            {
                minX = Math.Min(minX, card.x);
                minY = Math.Min(minY, card.y);
                maxX = Math.Max(maxX, card.x + card.w);
                maxY = Math.Max(maxY, card.y + card.h);
            }
            double padding = 160;
            return new Rect(minX - padding, minY - padding, Math.Max(1, maxX - minX + padding * 2), Math.Max(1, maxY - minY + padding * 2));
        }

        private Rect GetMinimapViewportBox()
        {
            if (minimapMetrics == null) return new Rect(0, 0, 0, 0);
            ViewportState viewport = CurrentViewport();
            return new Rect(
                minimapMetrics.OffsetX + (viewport.x - minimapMetrics.Bounds.Left) * minimapMetrics.Scale,
                minimapMetrics.OffsetY + (viewport.y - minimapMetrics.Bounds.Top) * minimapMetrics.Scale,
                (StageWidth() / viewport.scale) * minimapMetrics.Scale,
                (StageHeight() / viewport.scale) * minimapMetrics.Scale
            );
        }

        private void RenderScratchMinimap()
        {
            if (scratchMinimap == null || scratchMinimapWorld == null || !scratchOpen) return;
            scratchMinimapWorld.Children.Clear();
            Rect bounds = GetScratchWorldBounds();
            double inset = 9;
            double scale = Math.Min((scratchMinimap.Width - inset * 2) / Math.Max(1, bounds.Width), (scratchMinimap.Height - inset * 2) / Math.Max(1, bounds.Height));
            double offsetX = (scratchMinimap.Width - bounds.Width * scale) / 2;
            double offsetY = (scratchMinimap.Height - bounds.Height * scale) / 2;
            scratchMinimapMetrics = new MinimapMetrics { Bounds = bounds, Scale = scale, OffsetX = offsetX, OffsetY = offsetY };
            foreach (ScratchItem item in scratchItems)
            {
                Rectangle dot = new Rectangle();
                dot.RadiusX = 2;
                dot.RadiusY = 2;
                dot.Width = Math.Max(4, item.w * scale);
                dot.Height = Math.Max(3, item.h * scale);
                dot.Fill = item.kind == "image" ? Solid(255, 255, 255, 146) : new SolidColorBrush(Mix(TagColor(item.tag), Colors.White, 0.74, 220));
                Canvas.SetLeft(dot, offsetX + (item.x - bounds.Left) * scale);
                Canvas.SetTop(dot, offsetY + (item.y - bounds.Top) * scale);
                scratchMinimapWorld.Children.Add(dot);
            }
            Rect vp = GetScratchMinimapViewportBox();
            scratchMinimapViewport.Width = vp.Width;
            scratchMinimapViewport.Height = vp.Height;
            Canvas.SetLeft(scratchMinimapViewport, vp.Left);
            Canvas.SetTop(scratchMinimapViewport, vp.Top);
        }

        private Rect GetScratchWorldBounds()
        {
            double minX = scratchViewport.x;
            double minY = scratchViewport.y;
            double maxX = scratchViewport.x + ScratchPaneWidth() / scratchViewport.scale;
            double maxY = scratchViewport.y + StageHeight() / scratchViewport.scale;
            foreach (ScratchItem item in scratchItems)
            {
                minX = Math.Min(minX, item.x);
                minY = Math.Min(minY, item.y);
                maxX = Math.Max(maxX, item.x + item.w);
                maxY = Math.Max(maxY, item.y + item.h);
            }
            double padding = 120;
            return new Rect(minX - padding, minY - padding, Math.Max(1, maxX - minX + padding * 2), Math.Max(1, maxY - minY + padding * 2));
        }

        private Rect GetScratchMinimapViewportBox()
        {
            if (scratchMinimapMetrics == null) return new Rect(0, 0, 0, 0);
            return new Rect(
                scratchMinimapMetrics.OffsetX + (scratchViewport.x - scratchMinimapMetrics.Bounds.Left) * scratchMinimapMetrics.Scale,
                scratchMinimapMetrics.OffsetY + (scratchViewport.y - scratchMinimapMetrics.Bounds.Top) * scratchMinimapMetrics.Scale,
                (ScratchPaneWidth() / scratchViewport.scale) * scratchMinimapMetrics.Scale,
                (StageHeight() / scratchViewport.scale) * scratchMinimapMetrics.Scale
            );
        }

        private double StageWidth()
        {
            return MainPaneWidth();
        }

        private double ActualStageWidth()
        {
            return stage != null && stage.ActualWidth > 0 ? stage.ActualWidth : Math.Max(1, Width);
        }

        private double MainPaneWidth()
        {
            return Math.Max(360, mainPaneWidth);
        }

        private double ScratchPaneWidth()
        {
            return Math.Max(320, scratchPaneWidth);
        }

        private double StageHeight()
        {
            return stage != null && stage.ActualHeight > 0 ? stage.ActualHeight : Math.Max(1, Height - 36);
        }

        private void ResizeWindowFromPointer(Point point)
        {
            double dx = point.X - pointerStart.X;
            double dy = point.Y - pointerStart.Y;
            applyingWindowGeometry = true;
            try
            {
                if (windowResizeMode == "window-left-resize")
                {
                    double nextMain = Clamp(mainPaneWidthStart - dx, 360, 1800);
                    double actualDx = mainPaneWidthStart - nextMain;
                    mainPaneWidth = nextMain;
                    Left = windowResizeStart.Left + actualDx;
                    Width = MainPaneWidth() + (scratchOpen ? ScratchPaneWidth() : 0);
                }
                else if (windowResizeMode == "window-right-resize")
                {
                    mainPaneWidth = Clamp(mainPaneWidthStart + dx, 360, 1800);
                    Width = MainPaneWidth();
                }
                else if (windowResizeMode == "window-top-resize")
                {
                    double nextHeight = Clamp(windowResizeStart.Height - dy, MinHeight, 2400);
                    double actualDy = windowResizeStart.Height - nextHeight;
                    Top = windowResizeStart.Top + actualDy;
                    Height = nextHeight;
                }
                else if (windowResizeMode == "window-bottom-resize")
                {
                    Height = Clamp(windowResizeStart.Height + dy, MinHeight, 2400);
                }
            }
            finally { applyingWindowGeometry = false; }
            PositionFloatingUi();
            RenderMinimap();
            RenderScratchMinimap();
            ScheduleGlassRefresh();
        }

        private void FitPanesToTotalWidth(double totalWidth)
        {
            if (scratchOpen)
            {
                double total = Math.Max(680, totalWidth);
                double oldTotal = Math.Max(1, MainPaneWidth() + ScratchPaneWidth());
                double ratio = Clamp(MainPaneWidth() / oldTotal, 360 / total, (total - 320) / total);
                mainPaneWidth = Clamp(total * ratio, 360, total - 320);
                scratchPaneWidth = Math.Max(320, total - mainPaneWidth);
            }
            else
            {
                mainPaneWidth = Clamp(totalWidth, 360, 1800);
            }
        }

        private void SyncPaneWidthsToWindow()
        {
            if (applyingWindowGeometry || interaction == "window-resize" || interaction == "pane-split-resize" || interaction == "scratch-pane-resize") return;
            if (windowMode != "full") return;
            FitPanesToTotalWidth(ActualStageWidth());
        }

        private void MinimapMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (minimapMetrics == null) RenderMinimap();
            Point point = e.GetPosition(minimap);
            Rect vp = GetMinimapViewportBox();
            if (vp.Contains(point)) minimapDragOffset = new Point(point.X - vp.Left, point.Y - vp.Top);
            else minimapDragOffset = new Point(vp.Width * 0.5, vp.Height * 0.5);
            minimapDragging = true;
            minimap.CaptureMouse();
            MoveViewportFromMinimap(point);
            e.Handled = true;
        }

        private void MinimapMouseMove(object sender, MouseEventArgs e)
        {
            if (!minimapDragging || e.LeftButton != MouseButtonState.Pressed) return;
            MoveViewportFromMinimap(e.GetPosition(minimap));
        }

        private void MoveViewportFromMinimap(Point point)
        {
            if (minimapMetrics == null) return;
            ViewportState viewport = CurrentViewport();
            double viewportLeft = point.X - minimapDragOffset.X;
            double viewportTop = point.Y - minimapDragOffset.Y;
            viewport.x = (viewportLeft - minimapMetrics.OffsetX) / minimapMetrics.Scale + minimapMetrics.Bounds.Left;
            viewport.y = (viewportTop - minimapMetrics.OffsetY) / minimapMetrics.Scale + minimapMetrics.Bounds.Top;
            UpdateWorldTransform();
            RenderMinimap();
        }

        private void StopMinimapDrag(bool save)
        {
            if (!minimapDragging) return;
            minimapDragging = false;
            minimap.ReleaseMouseCapture();
            if (save) SaveStateSoon();
        }

        private void ScratchMinimapMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (scratchMinimapMetrics == null) RenderScratchMinimap();
            Point point = e.GetPosition(scratchMinimap);
            Rect vp = GetScratchMinimapViewportBox();
            if (vp.Contains(point)) scratchMinimapDragOffset = new Point(point.X - vp.Left, point.Y - vp.Top);
            else scratchMinimapDragOffset = new Point(vp.Width * 0.5, vp.Height * 0.5);
            scratchMinimapDragging = true;
            scratchMinimap.CaptureMouse();
            MoveScratchViewportFromMinimap(point);
            e.Handled = true;
        }

        private void ScratchMinimapMouseMove(object sender, MouseEventArgs e)
        {
            if (!scratchMinimapDragging || e.LeftButton != MouseButtonState.Pressed) return;
            MoveScratchViewportFromMinimap(e.GetPosition(scratchMinimap));
        }

        private void MoveScratchViewportFromMinimap(Point point)
        {
            if (scratchMinimapMetrics == null) return;
            double viewportLeft = point.X - scratchMinimapDragOffset.X;
            double viewportTop = point.Y - scratchMinimapDragOffset.Y;
            scratchViewport.x = (viewportLeft - scratchMinimapMetrics.OffsetX) / scratchMinimapMetrics.Scale + scratchMinimapMetrics.Bounds.Left;
            scratchViewport.y = (viewportTop - scratchMinimapMetrics.OffsetY) / scratchMinimapMetrics.Scale + scratchMinimapMetrics.Bounds.Top;
            UpdateScratchWorldTransform();
            RenderScratchMinimap();
        }

        private void StopScratchMinimapDrag()
        {
            if (!scratchMinimapDragging) return;
            scratchMinimapDragging = false;
            scratchMinimap.ReleaseMouseCapture();
        }

        private void StageDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (IsPointInScratchPane(e.GetPosition(stage)))
                {
                    string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                    e.Effects = paths != null && paths.Any(IsImageFile) ? DragDropEffects.Copy : DragDropEffects.None;
                }
                else e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void StageDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (IsPointInScratchPane(e.GetPosition(stage))) ImportScratchFiles(paths, StageToScratchPoint(e.GetPosition(stage)));
            else ImportFiles(paths, e.GetPosition(stage));
            e.Handled = true;
        }

        private void ImportFiles(string[] paths, Point screenPoint)
        {
            if (IsArchiveSide())
            {
                ShowToast("请回到正面录入");
                return;
            }
            Point start = ScreenToWorld(screenPoint);
            double offsetY = 0;
            int imported = 0;
            foreach (string path in paths)
            {
                try
                {
                    string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    string content = null;
                    if (ext == ".txt" || ext == ".md") content = ReadTextFile(path);
                    else if (ext == ".docx" || ext == ".docm" || ext == ".dotx") content = ReadDocx(path);
                    else if (ext == ".doc" || ext == ".rtf") content = ReadWordWithCom(path);
                    if (content == null) continue;
                    string title = System.IO.Path.GetFileNameWithoutExtension(path);
                    Size size = EstimateFileCardSize(title);
                    CreateCard(start.X, start.Y + offsetY, size.Width, size.Height, title, content);
                    offsetY += size.Height + 12;
                    imported++;
                }
                catch
                {
                    ShowToast("文件未导入");
                }
            }
            if (imported > 0) ShowToast("已录入");
            else ShowToast("仅支持 Word/TXT");
        }

        private Size EstimateFileCardSize(string title)
        {
            int length = Math.Max(4, title == null ? 0 : title.Length);
            return new Size(Clamp(length * 15 + 48, 128, 270), title != null && title.Length > 14 ? 82 : 66);
        }

        private string ReadTextFile(string path)
        {
            Encoding[] encodings = new Encoding[] { new UTF8Encoding(true), Encoding.UTF8, Encoding.GetEncoding("gbk") };
            foreach (Encoding encoding in encodings)
            {
                try { return File.ReadAllText(path, encoding); }
                catch { }
            }
            return File.ReadAllText(path);
        }

        private string ReadDocx(string path)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XDocument docXml;
            XDocument numberingXml = null;
            MemoryStream package = new MemoryStream();
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.CopyTo(package);
            }
            package.Position = 0;
            using (ZipArchive archive = new ZipArchive(package, ZipArchiveMode.Read))
            {
                ZipArchiveEntry docEntry = archive.GetEntry("word/document.xml");
                if (docEntry == null) throw new InvalidDataException("document.xml missing");
                using (Stream stream = docEntry.Open()) docXml = XDocument.Load(stream);
                ZipArchiveEntry numberingEntry = archive.GetEntry("word/numbering.xml");
                if (numberingEntry != null)
                {
                    using (Stream stream = numberingEntry.Open()) numberingXml = XDocument.Load(stream);
                }
            }
            Dictionary<string, Dictionary<string, string>> abstractFormats = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, string> numToAbstract = new Dictionary<string, string>();
            if (numberingXml != null)
            {
                foreach (XElement abstractNum in numberingXml.Descendants(w + "abstractNum"))
                {
                    string abstractId = Attr(abstractNum, w, "abstractNumId");
                    abstractFormats[abstractId] = new Dictionary<string, string>();
                    foreach (XElement lvl in abstractNum.Elements(w + "lvl"))
                    {
                        string level = Attr(lvl, w, "ilvl");
                        XElement numFmt = lvl.Element(w + "numFmt");
                        abstractFormats[abstractId][String.IsNullOrEmpty(level) ? "0" : level] = numFmt == null ? "bullet" : Attr(numFmt, w, "val");
                    }
                }
                foreach (XElement num in numberingXml.Descendants(w + "num"))
                {
                    string numId = Attr(num, w, "numId");
                    XElement abstractNumId = num.Element(w + "abstractNumId");
                    if (abstractNumId != null) numToAbstract[numId] = Attr(abstractNumId, w, "val");
                }
            }

            Dictionary<string, int> counters = new Dictionary<string, int>();
            List<string> lines = new List<string>();
            foreach (XElement p in docXml.Descendants(w + "p"))
            {
                StringBuilder text = new StringBuilder();
                foreach (XElement node in p.Descendants())
                {
                    if (node.Name == w + "t") text.Append(node.Value);
                    else if (node.Name == w + "tab") text.Append("\t");
                    else if (node.Name == w + "br") text.Append("\n");
                }
                string raw = text.ToString().TrimEnd();
                if (String.IsNullOrWhiteSpace(raw))
                {
                    lines.Add("");
                    continue;
                }
                XElement numPr = p.Descendants(w + "numPr").FirstOrDefault();
                if (numPr == null)
                {
                    lines.Add(raw);
                    continue;
                }
                string level = Attr(numPr.Descendants(w + "ilvl").FirstOrDefault(), w, "val");
                string numId = Attr(numPr.Descendants(w + "numId").FirstOrDefault(), w, "val");
                if (String.IsNullOrEmpty(level)) level = "0";
                string abstractId = numToAbstract.ContainsKey(numId) ? numToAbstract[numId] : "";
                string format = abstractFormats.ContainsKey(abstractId) && abstractFormats[abstractId].ContainsKey(level) ? abstractFormats[abstractId][level] : "bullet";
                int levelNumber = 0;
                Int32.TryParse(level, out levelNumber);
                string indent = new string(' ', levelNumber * 2);
                if (format == "decimal")
                {
                    string key = numId + ":" + level;
                    counters[key] = counters.ContainsKey(key) ? counters[key] + 1 : 1;
                    lines.Add(indent + counters[key] + ". " + raw);
                }
                else
                {
                    lines.Add(indent + "• " + raw);
                }
            }
            return Regex.Replace(String.Join("\n", lines.ToArray()), "\n{3,}", "\n\n").Trim();
        }

        private string Attr(XElement element, XNamespace ns, string name)
        {
            if (element == null) return "";
            XAttribute attr = element.Attribute(ns + name);
            if (attr == null) attr = element.Attribute(name);
            return attr == null ? "" : attr.Value;
        }

        private string ReadWordWithCom(string path)
        {
            Type wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null) throw new InvalidOperationException("Word not installed");
            object word = null;
            object doc = null;
            try
            {
                word = Activator.CreateInstance(wordType);
                wordType.InvokeMember("Visible", BindingFlags.SetProperty, null, word, new object[] { false });
                object docs = wordType.InvokeMember("Documents", BindingFlags.GetProperty, null, word, null);
                doc = docs.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, docs, new object[] { path, false, true });
                object content = doc.GetType().InvokeMember("Content", BindingFlags.GetProperty, null, doc, null);
                object text = content.GetType().InvokeMember("Text", BindingFlags.GetProperty, null, content, null);
                return text == null ? "" : text.ToString().Trim();
            }
            finally
            {
                if (doc != null)
                {
                    try { doc.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, doc, new object[] { false }); } catch { }
                    Marshal.ReleaseComObject(doc);
                }
                if (word != null)
                {
                    try { wordType.InvokeMember("Quit", BindingFlags.InvokeMethod, null, word, null); } catch { }
                    Marshal.ReleaseComObject(word);
                }
            }
        }

        private void ShowToast(string text)
        {
            toastText.Text = text;
            toast.Visibility = Visibility.Visible;
            toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            PositionFloatingUi();
            toastTimer.Stop();
            toastTimer.Start();
        }

        private void ClearOrganizeState()
        {
            state.organized = false;
            state.organizedSide = state.side;
            state.organizedSnapshot = null;
        }

        private LinboState LoadState()
        {
            try
            {
                if (File.Exists(statePath))
                {
                    LinboState loaded = serializer.Deserialize<LinboState>(File.ReadAllText(statePath, Encoding.UTF8));
                    return NormalizeState(loaded);
                }
            }
            catch { }
            return DefaultState();
        }

        private LinboState NormalizeState(LinboState loaded)
        {
            if (loaded == null) loaded = DefaultState();
            if (loaded.side != "archive") loaded.side = "front";
            if (loaded.viewports == null) loaded.viewports = DefaultViewports();
            if (!loaded.viewports.ContainsKey("front")) loaded.viewports["front"] = new ViewportState { x = 0, y = 0, scale = 1 };
            if (!loaded.viewports.ContainsKey("archive")) loaded.viewports["archive"] = new ViewportState { x = 0, y = 0, scale = 1 };
            foreach (ViewportState viewport in loaded.viewports.Values) viewport.scale = Clamp(viewport.scale <= 0 ? 1 : viewport.scale, 0.35, 2.4);
            loaded.mainPaneWidth = Clamp(loaded.mainPaneWidth <= 0 ? 585 : loaded.mainPaneWidth, 360, 1800);
            loaded.scratchPaneWidth = Clamp(loaded.scratchPaneWidth <= 0 ? loaded.mainPaneWidth : loaded.scratchPaneWidth, 320, 1800);
            if (loaded.windowWidth <= 0) loaded.windowWidth = loaded.scratchOpen ? loaded.mainPaneWidth + loaded.scratchPaneWidth : loaded.mainPaneWidth;
            if (loaded.windowHeight <= 0) loaded.windowHeight = 1040;
            if (loaded.windowNormalWidth <= 0) loaded.windowNormalWidth = loaded.windowWidth;
            if (loaded.windowNormalHeight <= 0) loaded.windowNormalHeight = loaded.windowHeight;
            if (loaded.windowMode != "vertical" && loaded.windowMode != "full") loaded.windowMode = "normal";
            if (loaded.cards == null) loaded.cards = new List<CardModel>();
            foreach (CardModel card in loaded.cards)
            {
                if (String.IsNullOrEmpty(card.id)) card.id = Guid.NewGuid().ToString("N");
                if (String.IsNullOrEmpty(card.tag) || !Tags.ContainsKey(card.tag)) card.tag = "gray";
                if (card.w <= 0) card.w = 160;
                if (card.h <= 0) card.h = 92;
                if (card.title == null) card.title = "";
                if (card.content == null) card.content = "";
                if (card.createdAt <= 0) card.createdAt = NowMs();
            }
            return loaded;
        }

        private LinboState DefaultState()
        {
            LinboState next = new LinboState();
            next.side = "front";
            next.viewports = DefaultViewports();
            next.minimapOpen = false;
            next.organized = false;
            next.organizedSide = "front";
            next.cards = new List<CardModel>();
            next.windowPlacementSaved = false;
            next.windowWidth = 585;
            next.windowHeight = 1040;
            next.windowNormalWidth = 585;
            next.windowNormalHeight = 1040;
            next.windowMode = "normal";
            next.mainPaneWidth = 585;
            next.scratchPaneWidth = 585;
            next.scratchOpen = false;
            return next;
        }

        private Dictionary<string, ViewportState> DefaultViewports()
        {
            return new Dictionary<string, ViewportState>
            {
                {"front", new ViewportState { x = 0, y = 0, scale = 1 }},
                {"archive", new ViewportState { x = 0, y = 0, scale = 1 }}
            };
        }

        private void SaveStateSoon()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void SaveStateNow()
        {
            try
            {
                SaveWindowPlacementToState();
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                File.WriteAllText(statePath, serializer.Serialize(state), Encoding.UTF8);
            }
            catch { }
        }

        private void SaveWindowPlacementToState()
        {
            if (state == null) return;
            if (windowMode == "normal") CaptureNormalWindowRect();
            state.windowPlacementSaved = true;
            state.windowLeft = Left;
            state.windowTop = Top;
            state.windowWidth = Width;
            state.windowHeight = Height;
            state.windowMode = windowMode;
            state.windowNormalLeft = normalWindowRect.Left;
            state.windowNormalTop = normalWindowRect.Top;
            state.windowNormalWidth = normalWindowRect.Width;
            state.windowNormalHeight = normalWindowRect.Height;
            state.mainPaneWidth = MainPaneWidth();
            state.scratchPaneWidth = ScratchPaneWidth();
            state.scratchOpen = scratchOpen;
        }

        private void ApplySavedWindowMode()
        {
            if (!state.windowPlacementSaved) return;
            if (windowMode == "vertical") ApplyVerticalMaximize();
            else if (windowMode == "full") ApplyFullMaximize();
        }

        private Color TagColor(string tag)
        {
            return Tags.ContainsKey(tag) ? Tags[tag].Color : Tags["gray"].Color;
        }

        public Brush TagBrush(string tag)
        {
            return new SolidColorBrush(TagColor(tag));
        }

        public Brush CardBorderBrush(string tag, bool selected)
        {
            return new SolidColorBrush(Mix(TagColor(tag), Colors.White, 0.50, selected ? (byte)154 : (byte)115));
        }

        private SolidColorBrush Solid(byte r, byte g, byte b, byte a)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        private SolidColorBrush BrushFrom(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private static Color Mix(Color left, Color right, double leftWeight, byte alpha)
        {
            double rightWeight = 1 - leftWeight;
            return Color.FromArgb(alpha,
                (byte)(left.R * leftWeight + right.R * rightWeight),
                (byte)(left.G * leftWeight + right.G * rightWeight),
                (byte)(left.B * leftWeight + right.B * rightWeight));
        }

        private double Clamp(double value, double min, double max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        private int ClampInt(int value, int min, int max)
        {
            if (max < min) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private double NowMs()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        private class MinimapMetrics
        {
            public Rect Bounds;
            public double Scale;
            public double OffsetX;
            public double OffsetY;
        }
    }

    public class CardControl : Border
    {
        private const double CopyGap = 10;
        private const double CopyOuterWidth = 46;
        private readonly LinboWindow app;
        public readonly CardModel Card;
        private Canvas root;
        private Border glass;
        private Grid glassRoot;
        private Image backdropImage;
        private Border chip;
        private TextBlock title;
        private TextBox editor;
        private Button copy;
        private Canvas resizeHandle;
        private bool selected;

        public CardControl(LinboWindow app, CardModel card)
        {
            this.app = app;
            Card = card;
            Width = card.w + CopyOuterWidth;
            Height = card.h;
            MinWidth = 146;
            MinHeight = 64;
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            ClipToBounds = false;
            Cursor = Cursors.Hand;

            root = new Canvas();
            Child = root;

            glass = new Border();
            glass.Width = card.w;
            glass.Height = card.h;
            glass.CornerRadius = new CornerRadius(8);
            glass.BorderThickness = new Thickness(1);
            glass.BorderBrush = app.CardBorderBrush(card.tag, false);
            glass.Background = new SolidColorBrush(Color.FromRgb(30, 30, 29));
            glass.Effect = CardShadow(false);
            glass.ClipToBounds = true;
            glassRoot = new Grid();
            glassRoot.Background = new SolidColorBrush(Color.FromRgb(30, 30, 29));

            backdropImage = new Image();
            backdropImage.Stretch = Stretch.Fill;
            backdropImage.Opacity = 0.98;
            backdropImage.Effect = new BlurEffect { Radius = 10, RenderingBias = RenderingBias.Quality };
            RenderOptions.SetBitmapScalingMode(backdropImage, BitmapScalingMode.LowQuality);
            glassRoot.Children.Add(backdropImage);

            Rectangle tint = new Rectangle();
            tint.Fill = new SolidColorBrush(Color.FromArgb(52, 30, 30, 29));
            glassRoot.Children.Add(tint);

            Rectangle fog = new Rectangle();
            fog.Fill = new SolidColorBrush(Color.FromArgb(10, 18, 18, 17));
            glassRoot.Children.Add(fog);

            Rectangle sheen = new Rectangle();
            sheen.Fill = new LinearGradientBrush(
                Color.FromArgb(20, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                145);
            glassRoot.Children.Add(sheen);

            Rectangle grain = new Rectangle();
            grain.Fill = CreateGrainBrush();
            grain.Opacity = 0.06;
            glassRoot.Children.Add(grain);

            Rectangle topHighlight = new Rectangle();
            topHighlight.Height = 1;
            topHighlight.VerticalAlignment = VerticalAlignment.Top;
            topHighlight.Fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            glassRoot.Children.Add(topHighlight);
            glass.Child = glassRoot;
            root.Children.Add(glass);
            Canvas.SetLeft(glass, 0);
            Canvas.SetTop(glass, 0);

            chip = new Border();
            chip.Width = 26;
            chip.Height = 4;
            chip.CornerRadius = new CornerRadius(2);
            chip.Background = app.TagBrush(card.tag);
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            chip.VerticalAlignment = VerticalAlignment.Top;
            chip.Margin = new Thickness(10, 9, 0, 0);
            glassRoot.Children.Add(chip);

            title = new TextBlock();
            title.Text = card.title ?? "";
            title.Foreground = new SolidColorBrush(Color.FromArgb(235, 250, 250, 246));
            title.FontSize = 16;
            title.LineHeight = 22;
            title.TextWrapping = TextWrapping.Wrap;
            title.Margin = new Thickness(12, 20, 38, 14);
            title.VerticalAlignment = VerticalAlignment.Stretch;
            title.HorizontalAlignment = HorizontalAlignment.Stretch;
            glassRoot.Children.Add(title);

            editor = new TextBox();
            editor.Text = card.title ?? "";
            editor.AcceptsReturn = true;
            editor.TextWrapping = TextWrapping.Wrap;
            editor.Foreground = title.Foreground;
            editor.FontSize = 16;
            editor.Margin = title.Margin;
            editor.BorderThickness = new Thickness(0);
            editor.Background = Brushes.Transparent;
            editor.Visibility = Visibility.Collapsed;
            editor.LostFocus += delegate { EndTitleEdit(true); };
            editor.KeyDown += EditorKeyDown;
            glassRoot.Children.Add(editor);

            copy = new Button();
            copy.Width = 26;
            copy.Height = 42;
            copy.Opacity = 0;
            copy.Cursor = Cursors.Hand;
            copy.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            copy.BorderBrush = new SolidColorBrush(Color.FromArgb(43, 255, 255, 255));
            copy.BorderThickness = new Thickness(1);
            copy.Template = CopyButtonTemplate();
            copy.Click += delegate { app.CopyCardContent(Card); };
            root.Children.Add(copy);
            Canvas.SetLeft(copy, card.w + CopyGap);
            Canvas.SetTop(copy, (card.h - 42) / 2);

            resizeHandle = new Canvas();
            resizeHandle.Width = 42;
            resizeHandle.Height = 42;
            resizeHandle.Background = Brushes.Transparent;
            resizeHandle.HorizontalAlignment = HorizontalAlignment.Right;
            resizeHandle.VerticalAlignment = VerticalAlignment.Bottom;
            resizeHandle.Cursor = Cursors.SizeNWSE;
            Line l1 = new Line { X1 = 24, Y1 = 33, X2 = 34, Y2 = 33, Stroke = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), StrokeThickness = 1 };
            Line l2 = new Line { X1 = 34, Y1 = 22, X2 = 34, Y2 = 33, Stroke = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), StrokeThickness = 1 };
            resizeHandle.Children.Add(l1);
            resizeHandle.Children.Add(l2);
            resizeHandle.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; app.BeginResize(Card, e); };
            glassRoot.Children.Add(resizeHandle);

            MouseEnter += delegate { FadeCopy(1, 120); };
            MouseLeave += delegate { FadeCopy(0, 820); };
            MouseLeftButtonDown += CardMouseLeftButtonDown;
            MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; app.ShowCardMenu(Card, PointToScreen(e.GetPosition(this))); };
            Loaded += delegate { RefreshGlass(); };
        }

        public void SetSelected(bool value)
        {
            selected = value;
            glass.BorderBrush = app.CardBorderBrush(Card.tag, selected);
            glass.Effect = CardShadow(selected);
        }

        public void UpdateSize()
        {
            Width = Card.w + CopyOuterWidth;
            Height = Card.h;
            glass.Width = Card.w;
            glass.Height = Card.h;
            Canvas.SetLeft(copy, Card.w + CopyGap);
            Canvas.SetTop(copy, (Card.h - 42) / 2);
            RefreshGlass();
        }

        public void RefreshGlass()
        {
            if (!IsLoaded || glass == null || glass.ActualWidth < 1 || glass.ActualHeight < 1) return;
            try
            {
                Point topLeft = glass.TransformToVisual(app.StageSurface).Transform(new Point(0, 0));
                Point bottomRight = glass.TransformToVisual(app.StageSurface).Transform(new Point(glass.ActualWidth, glass.ActualHeight));
                Rect stageRect = new Rect(topLeft, bottomRight);
                stageRect = new Rect(
                    Math.Min(stageRect.Left, stageRect.Right),
                    Math.Min(stageRect.Top, stageRect.Bottom),
                    Math.Abs(stageRect.Width),
                    Math.Abs(stageRect.Height));
                BitmapSource source = app.CaptureBackdropFor(this, stageRect);
                if (source == null) return;
                double longest = Math.Max(1, Math.Max(source.PixelWidth, source.PixelHeight));
                double scale = Math.Max(0.12, Math.Min(0.24, 54.0 / longest));
                TransformedBitmap small = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                small.Freeze();
                backdropImage.Source = small;
            }
            catch { }
        }

        private Brush CreateGrainBrush()
        {
            DrawingGroup group = new DrawingGroup();
            using (DrawingContext dc = group.Open())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), null, new Rect(0, 0, 1, 1));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)), null, new Rect(2, 1, 1, 1));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(8, 255, 255, 255)), null, new Rect(1, 3, 1, 1));
            }

            DrawingBrush brush = new DrawingBrush(group);
            brush.TileMode = TileMode.Tile;
            brush.Viewport = new Rect(0, 0, 4, 4);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Stretch = Stretch.None;
            return brush;
        }

        public void BeginTitleEdit()
        {
            editor.Text = Card.title ?? "";
            title.Visibility = Visibility.Collapsed;
            editor.Visibility = Visibility.Visible;
            editor.Focus();
            editor.SelectAll();
        }

        private void EndTitleEdit(bool save)
        {
            if (editor.Visibility != Visibility.Visible) return;
            if (save)
            {
                Card.title = editor.Text ?? "";
                title.Text = Card.title;
                app.ScheduleGlassRefresh();
            }
            editor.Visibility = Visibility.Collapsed;
            title.Visibility = Visibility.Visible;
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                EndTitleEdit(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                EndTitleEdit(true);
                e.Handled = true;
            }
        }

        private void CardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (copy.IsMouseOver || resizeHandle.IsMouseOver) return;
            e.Handled = true;
            if (e.ClickCount >= 2)
            {
                app.BeginTitleEdit(this);
                return;
            }
            app.BeginCardDrag(Card, e);
        }

        private void FadeCopy(double to, int ms)
        {
            copy.BeginAnimation(OpacityProperty, new DoubleAnimation(copy.Opacity, to, TimeSpan.FromMilliseconds(ms)));
        }

        private DropShadowEffect CardShadow(bool selected)
        {
            return new DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = selected ? 0.52 : 0.30,
                BlurRadius = selected ? 52 : 38,
                ShadowDepth = selected ? 10 : 8
            };
        }

        private ControlTemplate CopyButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory canvas = new FrameworkElementFactory(typeof(Canvas));
            canvas.SetValue(Canvas.WidthProperty, 18.0);
            canvas.SetValue(Canvas.HeightProperty, 18.0);
            FrameworkElementFactory r1 = new FrameworkElementFactory(typeof(Rectangle));
            r1.SetValue(Rectangle.WidthProperty, 11.0);
            r1.SetValue(Rectangle.HeightProperty, 14.0);
            r1.SetValue(Rectangle.RadiusXProperty, 3.0);
            r1.SetValue(Rectangle.RadiusYProperty, 3.0);
            r1.SetValue(Rectangle.StrokeProperty, new SolidColorBrush(Color.FromArgb(214, 255, 255, 255)));
            r1.SetValue(Rectangle.StrokeThicknessProperty, 1.5);
            r1.SetValue(Canvas.LeftProperty, 5.0);
            r1.SetValue(Canvas.TopProperty, 4.0);
            canvas.AppendChild(r1);
            FrameworkElementFactory r2 = new FrameworkElementFactory(typeof(Rectangle));
            r2.SetValue(Rectangle.WidthProperty, 11.0);
            r2.SetValue(Rectangle.HeightProperty, 14.0);
            r2.SetValue(Rectangle.RadiusXProperty, 3.0);
            r2.SetValue(Rectangle.RadiusYProperty, 3.0);
            r2.SetValue(Rectangle.StrokeProperty, new SolidColorBrush(Color.FromArgb(148, 255, 255, 255)));
            r2.SetValue(Rectangle.StrokeThicknessProperty, 1.2);
            r2.SetValue(Canvas.LeftProperty, 1.0);
            r2.SetValue(Canvas.TopProperty, 0.0);
            canvas.AppendChild(r2);
            border.AppendChild(canvas);
            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }
    }

    public class ScratchItemControl : Border
    {
        private const double CopyGap = 10;
        private const double CopyOuterWidth = 46;
        private readonly LinboWindow app;
        public readonly ScratchItem Item;
        private Canvas root;
        private FrameworkElement body;
        private Grid imageGrid;
        private Image imageElement;
        private Border selectionFrame;
        private InkCanvas ink;
        private Button copy;
        private List<Canvas> resizeHandles = new List<Canvas>();
        private bool selected;
        private bool hovering;
        private bool straightLineDrawing;
        private Point straightLineStart;
        private Stroke straightLineStroke;

        public ScratchItemControl(LinboWindow app, ScratchItem item)
        {
            this.app = app;
            Item = item;
            Width = item.kind == "mirror" ? item.w + CopyOuterWidth : item.w;
            Height = item.h;
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            ClipToBounds = false;
            Cursor = Cursors.Hand;

            root = new Canvas();
            Child = root;
            if (item.kind == "image") BuildImage();
            else BuildMirror();

            selectionFrame = new Border();
            selectionFrame.Width = item.w;
            selectionFrame.Height = item.h;
            selectionFrame.BorderThickness = new Thickness(1);
            selectionFrame.BorderBrush = new SolidColorBrush(Color.FromArgb(190, 220, 238, 255));
            selectionFrame.Background = Brushes.Transparent;
            selectionFrame.Visibility = Visibility.Collapsed;
            selectionFrame.IsHitTestVisible = false;
            root.Children.Add(selectionFrame);

            AddResizeHandle("nw");
            AddResizeHandle("ne");
            AddResizeHandle("sw");
            AddResizeHandle("se");
            UpdateHandlePositions();

            MouseLeftButtonDown += ScratchMouseLeftButtonDown;
            MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                app.ShowScratchItemMenu(Item, PointToScreen(e.GetPosition(this)));
            };
            MouseEnter += delegate { if (copy != null) FadeCopy(1, 120); };
            MouseEnter += delegate { hovering = true; RefreshSelectionChrome(); };
            MouseLeave += delegate { if (copy != null) FadeCopy(0, 620); hovering = false; RefreshSelectionChrome(); };
        }

        private void BuildImage()
        {
            imageGrid = new Grid();
            imageGrid.Width = Item.w;
            imageGrid.Height = Item.h;
            imageElement = new Image();
            imageElement.Source = Item.image;
            imageElement.Width = Item.w;
            imageElement.Height = Item.h;
            imageElement.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(imageElement, BitmapScalingMode.HighQuality);
            imageGrid.Children.Add(imageElement);

            ink = new InkCanvas();
            ink.Width = Item.w;
            ink.Height = Item.h;
            ink.Background = Brushes.Transparent;
            ink.EditingMode = InkCanvasEditingMode.None;
            ink.IsHitTestVisible = false;
            if (Item.strokes == null) Item.strokes = new StrokeCollection();
            ink.Strokes = Item.strokes;
            ink.PreviewMouseLeftButtonDown += InkPreviewMouseLeftButtonDown;
            ink.PreviewMouseMove += InkPreviewMouseMove;
            ink.PreviewMouseLeftButtonUp += InkPreviewMouseLeftButtonUp;
            imageGrid.Children.Add(ink);

            body = imageGrid;
            root.Children.Add(imageGrid);
        }

        private void BuildMirror()
        {
            Border glass = new Border();
            glass.Width = Item.w;
            glass.Height = Item.h;
            glass.CornerRadius = new CornerRadius(8);
            glass.BorderThickness = new Thickness(1);
            glass.BorderBrush = app.CardBorderBrush(Item.tag, false);
            glass.Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 29));
            glass.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.30, BlurRadius = 38, ShadowDepth = 8 };
            Grid grid = new Grid();
            Rectangle sheen = new Rectangle();
            sheen.Fill = new LinearGradientBrush(Color.FromArgb(18, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 145);
            grid.Children.Add(sheen);
            Border chip = new Border();
            chip.Width = 26;
            chip.Height = 4;
            chip.CornerRadius = new CornerRadius(2);
            chip.Background = app.TagBrush(Item.tag);
            chip.Margin = new Thickness(10, 9, 0, 0);
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            chip.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(chip);
            TextBlock title = new TextBlock();
            title.Text = Item.title ?? "";
            title.Foreground = new SolidColorBrush(Color.FromArgb(235, 250, 250, 246));
            title.FontSize = 16;
            title.LineHeight = 22;
            title.TextWrapping = TextWrapping.Wrap;
            title.Margin = new Thickness(12, 20, 38, 14);
            grid.Children.Add(title);
            glass.Child = grid;
            body = glass;
            root.Children.Add(glass);

            copy = new Button();
            copy.Width = 26;
            copy.Height = 42;
            copy.Opacity = 0;
            copy.Cursor = Cursors.Hand;
            copy.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            copy.BorderBrush = new SolidColorBrush(Color.FromArgb(43, 255, 255, 255));
            copy.BorderThickness = new Thickness(1);
            copy.Template = CopyButtonTemplate();
            copy.Click += delegate { app.CopyScratchItem(Item); };
            root.Children.Add(copy);
            Canvas.SetLeft(copy, Item.w + CopyGap);
            Canvas.SetTop(copy, (Item.h - 42) / 2);
        }

        public void UpdateSize()
        {
            double previousWidth = Math.Max(1, Width - (Item.kind == "mirror" ? CopyOuterWidth : 0));
            double previousHeight = Math.Max(1, Height);
            Width = Item.kind == "mirror" ? Item.w + CopyOuterWidth : Item.w;
            Height = Item.h;
            if (body != null)
            {
                body.Width = Item.w;
                body.Height = Item.h;
            }
            if (imageGrid != null)
            {
                imageGrid.Width = Item.w;
                imageGrid.Height = Item.h;
            }
            if (imageElement != null)
            {
                imageElement.Width = Item.w;
                imageElement.Height = Item.h;
            }
            if (selectionFrame != null)
            {
                selectionFrame.Width = Item.w;
                selectionFrame.Height = Item.h;
            }
            if (ink != null)
            {
                if (Item.strokes != null && previousWidth > 0 && previousHeight > 0 && Math.Abs(previousWidth - Item.w) + Math.Abs(previousHeight - Item.h) > 0.01)
                {
                    Matrix matrix = new Matrix();
                    matrix.Scale(Item.w / previousWidth, Item.h / previousHeight);
                    Item.strokes.Transform(matrix, false);
                }
                ink.Width = Item.w;
                ink.Height = Item.h;
            }
            UpdateHandlePositions();
            if (copy != null)
            {
                Canvas.SetLeft(copy, Item.w + CopyGap);
                Canvas.SetTop(copy, (Item.h - 42) / 2);
            }
        }

        private void ScratchMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ink != null && ink.IsHitTestVisible && ink.IsMouseOver) return;
            if (copy != null && copy.IsMouseOver) return;
            if (resizeHandles.Any(h => h.IsMouseOver)) return;
            e.Handled = true;
            app.BeginScratchItemDrag(Item, e);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            RefreshSelectionChrome();
        }

        public void SetDrawing(bool value, bool eraser)
        {
            if (ink == null) return;
            ink.EditingMode = value ? (eraser ? InkCanvasEditingMode.EraseByPoint : InkCanvasEditingMode.Ink) : InkCanvasEditingMode.None;
            ink.IsHitTestVisible = value;
        }

        public void SetDrawingAttributes(Color color, double size)
        {
            if (ink == null) return;
            ink.DefaultDrawingAttributes.Color = color;
            ink.DefaultDrawingAttributes.Width = size;
            ink.DefaultDrawingAttributes.Height = size;
            ink.DefaultDrawingAttributes.FitToCurve = true;
            ink.EraserShape = new EllipseStylusShape(size + 4, size + 4);
        }

        private void InkPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!CanDrawStraightLine()) return;
            straightLineDrawing = true;
            straightLineStart = e.GetPosition(ink);
            straightLineStroke = CreateStraightLineStroke(straightLineStart);
            ink.Strokes.Add(straightLineStroke);
            ink.CaptureMouse();
            e.Handled = true;
        }

        private void InkPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!straightLineDrawing) return;
            ReplaceStraightLineStroke(e.GetPosition(ink));
            e.Handled = true;
        }

        private void InkPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!straightLineDrawing) return;
            ReplaceStraightLineStroke(e.GetPosition(ink));
            straightLineDrawing = false;
            straightLineStroke = null;
            ink.ReleaseMouseCapture();
            e.Handled = true;
        }

        private bool CanDrawStraightLine()
        {
            return ink != null
                && ink.IsHitTestVisible
                && ink.EditingMode == InkCanvasEditingMode.Ink
                && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        }

        private void ReplaceStraightLineStroke(Point end)
        {
            if (straightLineStroke != null) ink.Strokes.Remove(straightLineStroke);
            straightLineStroke = CreateStraightLineStroke(end);
            ink.Strokes.Add(straightLineStroke);
        }

        private Stroke CreateStraightLineStroke(Point end)
        {
            StylusPointCollection points = new StylusPointCollection();
            points.Add(new StylusPoint(straightLineStart.X, straightLineStart.Y));
            points.Add(new StylusPoint(end.X, end.Y));
            DrawingAttributes attributes = ink.DefaultDrawingAttributes.Clone();
            attributes.FitToCurve = false;
            return new Stroke(points, attributes);
        }

        private void RefreshSelectionChrome()
        {
            bool show = selected || hovering;
            if (selectionFrame != null) selectionFrame.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            foreach (Canvas handle in resizeHandles) handle.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddResizeHandle(string mode)
        {
            Canvas handle = CreateResizeHandle(mode);
            handle.Visibility = Visibility.Collapsed;
            handle.Tag = mode;
            handle.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                app.BeginScratchItemResize(Item, e, mode);
            };
            resizeHandles.Add(handle);
            root.Children.Add(handle);
        }

        private void UpdateHandlePositions()
        {
            foreach (Canvas handle in resizeHandles)
            {
                string mode = handle.Tag as string;
                double x = mode != null && mode.Contains("e") ? Item.w - 11 : -7;
                double y = mode != null && mode.Contains("s") ? Item.h - 11 : -7;
                Canvas.SetLeft(handle, x);
                Canvas.SetTop(handle, y);
            }
        }

        private Canvas CreateResizeHandle(string mode)
        {
            Canvas handle = new Canvas();
            handle.Width = 18;
            handle.Height = 18;
            handle.Background = Brushes.Transparent;
            handle.Cursor = Cursors.SizeNWSE;
            Ellipse dot = new Ellipse();
            dot.Width = 8;
            dot.Height = 8;
            dot.Fill = new SolidColorBrush(Color.FromArgb(214, 228, 240, 255));
            dot.Stroke = new SolidColorBrush(Color.FromArgb(150, 22, 24, 26));
            dot.StrokeThickness = 1;
            Canvas.SetLeft(dot, 5);
            Canvas.SetTop(dot, 5);
            handle.Children.Add(dot);
            return handle;
        }

        private void FadeCopy(double to, int ms)
        {
            copy.BeginAnimation(OpacityProperty, new DoubleAnimation(copy.Opacity, to, TimeSpan.FromMilliseconds(ms)));
        }

        public BitmapSource RenderContentBitmap()
        {
            if (body == null || Item.kind != "image") return null;
            try
            {
                int width = Math.Max(1, (int)Math.Ceiling(Item.w));
                int height = Math.Max(1, (int)Math.Ceiling(Item.h));
                RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(body);
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        private ControlTemplate CopyButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory canvas = new FrameworkElementFactory(typeof(Canvas));
            canvas.SetValue(Canvas.WidthProperty, 18.0);
            canvas.SetValue(Canvas.HeightProperty, 18.0);
            FrameworkElementFactory r1 = new FrameworkElementFactory(typeof(Rectangle));
            r1.SetValue(Rectangle.WidthProperty, 11.0);
            r1.SetValue(Rectangle.HeightProperty, 14.0);
            r1.SetValue(Rectangle.RadiusXProperty, 3.0);
            r1.SetValue(Rectangle.RadiusYProperty, 3.0);
            r1.SetValue(Rectangle.StrokeProperty, new SolidColorBrush(Color.FromArgb(214, 255, 255, 255)));
            r1.SetValue(Rectangle.StrokeThicknessProperty, 1.5);
            r1.SetValue(Canvas.LeftProperty, 5.0);
            r1.SetValue(Canvas.TopProperty, 4.0);
            canvas.AppendChild(r1);
            FrameworkElementFactory r2 = new FrameworkElementFactory(typeof(Rectangle));
            r2.SetValue(Rectangle.WidthProperty, 11.0);
            r2.SetValue(Rectangle.HeightProperty, 14.0);
            r2.SetValue(Rectangle.RadiusXProperty, 3.0);
            r2.SetValue(Rectangle.RadiusYProperty, 3.0);
            r2.SetValue(Rectangle.StrokeProperty, new SolidColorBrush(Color.FromArgb(148, 255, 255, 255)));
            r2.SetValue(Rectangle.StrokeThicknessProperty, 1.2);
            r2.SetValue(Canvas.LeftProperty, 1.0);
            r2.SetValue(Canvas.TopProperty, 0.0);
            canvas.AppendChild(r2);
            border.AppendChild(canvas);
            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }
    }

    public class TagDialog : Window
    {
        public string SelectedTag { get; private set; }

        public TagDialog(Window owner, string current, Dictionary<string, TagInfo> tags)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = 360;
            Height = 126;
            SelectedTag = current;

            Border shell = DialogShell();
            StackPanel content = new StackPanel { Margin = new Thickness(18) };
            content.Children.Add(new TextBlock { Text = "标签", FontSize = 16, Foreground = new SolidColorBrush(Color.FromArgb(235, 250, 250, 246)), Margin = new Thickness(0, 0, 0, 14) });
            UniformGrid grid = new UniformGrid { Columns = 4 };
            foreach (KeyValuePair<string, TagInfo> pair in tags)
            {
                Button button = new Button();
                button.Height = 42;
                button.Margin = new Thickness(0, 0, 8, 0);
                button.Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255));
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(31, 255, 255, 255));
                button.BorderThickness = new Thickness(1);
                button.Template = RoundedTemplate(8);
                Border chip = new Border { Width = 22, Height = 5, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(pair.Value.Color) };
                button.Content = chip;
                string key = pair.Key;
                button.Click += delegate { SelectedTag = key; DialogResult = true; };
                grid.Children.Add(button);
            }
            content.Children.Add(grid);
            shell.Child = content;
            Content = shell;
        }

        public static Border DialogShell()
        {
            Border border = new Border();
            border.CornerRadius = new CornerRadius(12);
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            border.Background = new SolidColorBrush(Color.FromArgb(230, 18, 18, 19));
            border.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.58, BlurRadius = 45, ShadowDepth = 12 };
            return border;
        }

        public static ControlTemplate RoundedTemplate(double radius)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }
    }

    public class PasteDialog : Window
    {
        private TextBox box;
        public string Value { get { return box.Text; } }

        public PasteDialog(Window owner, string initial)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = 360;
            Height = 340;
            Border shell = TagDialog.DialogShell();
            StackPanel content = new StackPanel { Margin = new Thickness(18) };
            content.Children.Add(new TextBlock { Text = "粘贴内容", FontSize = 16, Foreground = new SolidColorBrush(Color.FromArgb(235, 250, 250, 246)), Margin = new Thickness(0, 0, 0, 14) });
            box = new TextBox();
            box.Text = initial ?? "";
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.Height = 220;
            box.Padding = new Thickness(12);
            box.Background = new SolidColorBrush(Color.FromArgb(62, 0, 0, 0));
            box.Foreground = new SolidColorBrush(Color.FromArgb(230, 250, 250, 246));
            box.BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            content.Children.Add(box);
            content.Children.Add(DialogButtons("保存"));
            shell.Child = content;
            Content = shell;
        }

        private UIElement DialogButtons(string ok)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            Button cancel = SmallButton("取消", false);
            cancel.Click += delegate { DialogResult = false; };
            Button save = SmallButton(ok, true);
            save.Click += delegate { DialogResult = true; };
            row.Children.Add(cancel);
            row.Children.Add(save);
            return row;
        }

        public static Button SmallButton(string text, bool primary)
        {
            Button button = new Button();
            button.Content = text;
            button.Height = 34;
            button.MinWidth = 68;
            button.Margin = new Thickness(8, 0, 0, 0);
            button.Padding = new Thickness(14, 0, 14, 0);
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            button.BorderThickness = new Thickness(1);
            button.Background = primary ? new SolidColorBrush(Color.FromArgb(224, 233, 229, 220)) : new SolidColorBrush(Color.FromArgb(13, 255, 255, 255));
            button.Foreground = primary ? Brushes.Black : new SolidColorBrush(Color.FromArgb(225, 248, 248, 244));
            button.Template = TagDialog.RoundedTemplate(17);
            return button;
        }
    }

    public class ConfirmDialog : Window
    {
        public ConfirmDialog(Window owner, string title, string body, string okText)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = 360;
            Height = 178;
            Border shell = TagDialog.DialogShell();
            StackPanel content = new StackPanel { Margin = new Thickness(18) };
            content.Children.Add(new TextBlock { Text = title, FontSize = 16, Foreground = new SolidColorBrush(Color.FromArgb(235, 250, 250, 246)), Margin = new Thickness(0, 0, 0, 14) });
            content.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(143, 248, 248, 244)), Margin = new Thickness(0, 0, 0, 20) });
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = PasteDialog.SmallButton("取消", false);
            cancel.Click += delegate { DialogResult = false; };
            Button ok = PasteDialog.SmallButton(okText, true);
            ok.Click += delegate { DialogResult = true; };
            row.Children.Add(cancel);
            row.Children.Add(ok);
            content.Children.Add(row);
            shell.Child = content;
            Content = shell;
        }
    }
}
