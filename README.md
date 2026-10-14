<p align="center">
  <img src="src/assets/PicRope.png" width="104" height="104" alt="图绳 Logo">
</p>

<h1 align="center">PicRope · 图绳</h1>

<p align="center"><strong>把截图挂起来，随手取用。</strong></p>

<p align="center">Windows 10/11 · 免费开源 · 中文界面 · 图片本地处理</p>

<p align="center">
  <a href="https://github.com/Renly1994/PicRope/releases/download/v1.0.0/PicRope-1.0.0-windows-x64.zip">下载 Windows 版</a>
  · <a href="#开始使用">开始使用</a>
  · <a href="https://github.com/Renly1994/PicRope/issues">反馈问题</a>
</p>

![图绳产品插画：木夹把图片挂在绳上，参考图用图钉固定](docs/assets/picrope-banner.png)

刚截的图，下一秒就要用。图绳把截图挂在屏幕上沿，单击复制，拖拽取用；需要反复对照的图片，还能钉在桌面上。

写文档、做教程、整理参考图时，把图片留在手边。鼠标移到屏幕上沿就能展开绳子，离开后自动收起，全屏时也会收起。

## 截图留在手边

![图绳实际界面：屏幕上沿挂着工作流程、临时笔记和界面参考三张示例图片](docs/assets/rope-preview.png)

- 截图自动挂上绳子，需要时直接复制或拖到目标应用，也能手动添加已有图片。
- 把参考图取下，钉成置顶桌面贴图，一边看图一边做；拖动调整位置，滚轮缩放。
- 长按图片即可编辑，画框、加箭头、写文字，或裁剪、旋转后保存。编辑过程在本机完成。
- 图片会轻轻摆动，滚轮可以浏览更多截图。托盘里可切换外观，按习惯开启剪贴板收集和开机启动。

## 开始使用

1. [下载 1.0.0 安装包](https://github.com/Renly1994/PicRope/releases/download/v1.0.0/PicRope-1.0.0-windows-x64.zip)，完整解压后双击 `Install.cmd`。
2. 启动后，用 `Win+PrtScn` 截图。保存到 Windows 截图文件夹的新图片会自动挂上。
3. 如果常用 `Win+Shift+S`、`PrtScn` 或微信 `Alt+A`，在托盘右键菜单中开启「收集剪贴板截图」。这个选项默认关闭。

支持 Windows 10/11 64 位，需要 .NET Framework 4.8 和 Microsoft Edge WebView2 Runtime。编辑器提示缺少运行时时，可从 [微软官网](https://developer.microsoft.com/microsoft-edge/webview2/) 安装 WebView2。

## 常用操作

| 想做什么 | 操作 |
| --- | --- |
| 展开、收起绳子 | 鼠标移到屏幕上沿、点击托盘图标，或按 `Ctrl+Alt+T` |
| 复制图片 | 单击图片，同时复制图片与文件 |
| 把图放进其他应用 | 拖拽图片；效果由目标应用决定 |
| 打开或编辑 | 双击用默认图片应用打开；长按或右键「编辑图片」进入编辑器 |
| 钉在桌面上 | 单击夹子取下图片；单击贴图上沿的图钉，重新挂回绳子 |
| 保存一份副本 | 右键「保存副本」，选择保存位置 |
| 取下图片 | 点击左上角叉号；托盘「全部取下」保留所有原文件 |

点击叉号时，图绳收集目录中的副本会进入回收站，手动添加的外部图片只从绳子上取下。拖拽移动完成后，图片会自动取下。

## 关于你的图片

截图和编辑内容在本机处理，无需上传。图片支持 PNG、JPG/JPEG、BMP、GIF、TIF/TIFF；GIF、TIFF 等多帧图片只编辑第一帧。模糊和像素化作用于整张图片。

图绳保留最近 50 张图片的展示记录，超出数量的旧图片仅从绳子上取下，文件仍然保留。设置与收集副本保存在程序旁的 `data` 文件夹中。

安装后，桌面会有 PicRope 快捷方式。可在 Windows「已安装的应用」中卸载；卸载会清理图绳收集的副本、设置和缓存。

## 致谢与许可

图绳参考了 [Tendedero](https://github.com/alejandrobujan/tendedero) 的截图晾衣绳创意，使用独立名称与图标。项目许可与来源声明见 [LICENSE](LICENSE) 和 [NOTICE](NOTICE)，第三方许可随安装包提供。
