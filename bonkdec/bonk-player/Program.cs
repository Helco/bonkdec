using System;
using System.Threading;
using Silk.NET.OpenAL;
using Veldrid;
using Veldrid.StartupUtilities;

var window = VeldridStartup.CreateWindow(new WindowCreateInfo
{
    X = 100,
    Y = 100,
    WindowWidth = 1024 * 3 / 2,
    WindowHeight = 768 * 3 / 2,
    WindowTitle = "Bonk-Player"
});

using var graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, new GraphicsDeviceOptions
{
    PreferDepthRangeZeroToOne = true,
    PreferStandardClipSpaceYDirection = true,
    SyncToVerticalBlank = true,
    Debug = true
}, GraphicsBackend.Vulkan);


window.Resized += () => graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);

window.KeyDown += (ev) =>
{
    if (ev.Repeat)
        return;
    switch(ev.Key)
    {
        case Key.Escape: window.Close(); break;
    }
};

var alc = ALContext.GetApi();
var al = AL.GetApi();
void CheckAL()
{
    var error = al!.GetError();
    if (error == AudioError.NoError)
        return;
    throw new Exception(error.ToString());
}
unsafe void OpenAL()
{
    var device = alc.OpenDevice(""); CheckAL();
    var context = alc.CreateContext(device, null); CheckAL();
    alc.MakeContextCurrent(context); CheckAL();
}
OpenAL();

using var player = new Bonk.Player(graphicsDevice, @"C:\dev\zanzarah\Resources\Videos\_v000.bik");

var time = new System.Diagnostics.Stopwatch();
time.Start();
while (window.Exists)
{
    var frameStart = time.Elapsed.TotalSeconds;

    player.Render(graphicsDevice.SwapchainFramebuffer);

    graphicsDevice.SwapBuffers();
    window.PumpEvents();

    var frameDuration = time.Elapsed.TotalSeconds - frameStart;
    var delay = (int)((1.0 / 60) - frameDuration * 1000 + 0.5);
    if (delay > 0)
        Thread.Sleep(delay);
}

player.Dispose(); // ensure destroy order before graphicsDevice
