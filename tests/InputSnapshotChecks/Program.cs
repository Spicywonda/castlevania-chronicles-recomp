using System.Reflection;
using RecompOne.Runtime.Hardware;
var manager = typeof(Controller).Assembly.GetType("RecompOne.Runtime.Host.InputManager")!;
var flags = BindingFlags.NonPublic | BindingFlags.Static;
// The game thread may read at this exact boundary while the host is still
// collecting input. Held gamepad buttons must not appear released here.
Controller.State = (ushort)(0xffff ^ Controller.Start);
Controller.State2 = (ushort)(0xffff ^ Controller.Cross);
manager.GetMethod("PollKeyboard", flags)!.Invoke(null, null);
if (Controller.State != (ushort)(0xffff ^ Controller.Start) ||
    Controller.State2 != (ushort)(0xffff ^ Controller.Cross))
    throw new Exception("Keyboard collection published a transient release of held gamepad buttons");
Console.WriteLine("PASS Keyboard collection preserves the published buttons of both ports");
// With no keyboard or gamepad, a completed poll must publish the release.
manager.GetMethod("Poll", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
if (Controller.State != 0xffff || Controller.State2 != 0xffff)
    throw new Exception("Completed poll did not release disconnected inputs");
Console.WriteLine("PASS Completed input poll publishes released buttons for disconnected devices");
