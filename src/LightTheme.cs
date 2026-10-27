using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ScreenLine
{
    internal static class LightTheme
    {
        static readonly ResourceDictionary styles = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='Action' TargetType='Button'>
    <Setter Property='FontFamily' Value='Segoe UI'/><Setter Property='FontSize' Value='13'/>
    <Setter Property='Foreground' Value='{DynamicResource Ui.TextBody}'/><Setter Property='Background' Value='{DynamicResource Ui.Surface}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Ui.BorderStrong}'/><Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='14,0'/><Setter Property='Height' Value='34'/><Setter Property='MinWidth' Value='80'/>
    <Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
      <Border x:Name='Surface' CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
        <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='{DynamicResource Ui.Hover}'/></Trigger>
        <Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Background' Value='{DynamicResource Ui.Pressed}'/></Trigger>
        <Trigger Property='Tag' Value='primary'><Setter Property='Background' Value='{DynamicResource Ui.Accent}'/><Setter Property='BorderBrush' Value='{DynamicResource Ui.Accent}'/><Setter Property='Foreground' Value='{DynamicResource Ui.White}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger>
        <MultiTrigger><MultiTrigger.Conditions><Condition Property='Tag' Value='primary'/><Condition Property='IsMouseOver' Value='True'/></MultiTrigger.Conditions><Setter TargetName='Surface' Property='Background' Value='{DynamicResource Ui.AccentHover}'/></MultiTrigger>
        <Trigger Property='Tag' Value='quiet'><Setter Property='BorderBrush' Value='Transparent'/><Setter Property='Background' Value='Transparent'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='{DynamicResource Ui.Focus}'/><Setter TargetName='Surface' Property='BorderThickness' Value='2'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Surface' Property='Opacity' Value='0.4'/><Setter Property='Cursor' Value='Arrow'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='Hint' TargetType='ToolTip'>
    <Setter Property='FontFamily' Value='Segoe UI'/><Setter Property='FontSize' Value='12'/><Setter Property='Foreground' Value='{DynamicResource Ui.TextSecondary}'/>
    <Setter Property='MaxWidth' Value='320'/><Setter Property='HasDropShadow' Value='False'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToolTip'>
      <Border Background='{DynamicResource Ui.SurfaceRaised}' BorderBrush='{DynamicResource Ui.Border}' BorderThickness='1' CornerRadius='7' Padding='10,7' Margin='8'>
        <Border.Effect><DropShadowEffect BlurRadius='12' ShadowDepth='2' Opacity='0.12'/></Border.Effect>
        <TextBlock Text='{TemplateBinding Content}' TextWrapping='Wrap'/>
      </Border>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='Bar' TargetType='ScrollBar'>
    <Setter Property='Background' Value='Transparent'/><Setter Property='Width' Value='10'/><Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
      <Grid Background='Transparent'>
        <Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True'
               Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' ViewportSize='{TemplateBinding ViewportSize}'
               Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
          <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False' Opacity='0' Background='Transparent'/></Track.DecreaseRepeatButton>
          <Track.Thumb><Thumb>
            <Thumb.Template><ControlTemplate TargetType='Thumb'>
              <Border x:Name='Grip' Background='{DynamicResource Ui.ScrollThumb}' CornerRadius='4' Margin='2'/>
              <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Grip' Property='Background' Value='{DynamicResource Ui.ScrollThumbHover}'/></Trigger>
                <Trigger Property='IsDragging' Value='True'><Setter TargetName='Grip' Property='Background' Value='{DynamicResource Ui.ScrollThumbHover}'/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Thumb.Template>
          </Thumb></Track.Thumb>
          <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False' Opacity='0' Background='Transparent'/></Track.IncreaseRepeatButton>
        </Track>
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property='Orientation' Value='Horizontal'><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
    <Style.Triggers>
      <Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='10'/></Trigger>
    </Style.Triggers>
  </Style>
</ResourceDictionary>");

        public static Style ButtonStyle { get { return (Style)styles["Action"]; } }
        public static Style HintStyle { get { return (Style)styles["Hint"]; } }
        public static SolidColorBrush Brush(string value) { return (SolidColorBrush)new BrushConverter().ConvertFromString(value); }

        public static void Install(Application app)
        {
            UiTheme.Install(app);
            app.Resources[typeof(ToolTip)] = styles["Hint"];
            app.Resources[typeof(ScrollBar)] = styles["Bar"];
        }

        public static UIElement CloseIcon()
        {
            return UiTheme.Paint(new System.Windows.Shapes.Path {
                Width = 8, Height = 8, Data = Geometry.Parse("M0,0 L8,8 M8,0 L0,8"),
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            }, System.Windows.Shapes.Shape.StrokeProperty, "Ui.TextBody");
        }

        public static Button Button(string title, Action action, string tag = null)
        {
            var button = new Button { Content = title, Style = ButtonStyle, Tag = tag };
            button.Click += delegate { action(); };
            return button;
        }

        public static Border Card(UIElement content)
        {
            var card = new Border {
                Child = content, Margin = new Thickness(16), Padding = new Thickness(24), CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = Color.FromRgb(38, 57, 76), BlurRadius = 22, ShadowDepth = 6, Opacity = 0.16 }
            };
            UiTheme.Paint(card, Border.BackgroundProperty, "Ui.Surface");
            UiTheme.Paint(card, Border.BorderBrushProperty, "Ui.Border");
            return card;
        }
    }
}
