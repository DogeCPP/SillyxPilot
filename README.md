# SillyxPilot ✈️

The xPilot plugin that yells at you when a supervisor slides into your DMs.

## Why this exists

I got suspended on VATSIM once. Not for flying badly. For being away from my keyboard
when a supervisor messaged me. I never saw the message, they assumed I was ignoring them,
and that was that.

So I built the thing I wish I had that day: a little plugin that watches my messages and
absolutely refuses to let me miss the important ones. If a supervisor contacts me, my
whole desk lights up. Loud sound, popup, Discord ping. There is no sleeping through it.

## What it does

* **Supervisor panic.** The moment a supervisor sends you a private message, it fires
  everything you have turned on: sound, desktop popup, and a Discord webhook ping
  (great for getting it on your phone).
* **Supervisor counter.** Shows how many supervisors are online right now, counted the
  same way VATSIM Radar counts, so the numbers actually match.
* **Chatlog.** Every private message, ATC message, radio call and broadcast, color coded
  and saved to disk. If someone contacted you, you have the receipt.
* **Live map.** You and the traffic around you, drawn as little green 777s. Every plane
  is a 777. Yes, even the Cessnas. That is the joke.
* **Airspace prediction.** Looks along your route and tells you which FIR is coming up
  and whether somebody is actually staffing it.
* **SillyPilot Network.** A tiny chat so people running the plugin can say hi to each
  other. Goes through a shared Discord channel, with a username filter so nobody ruins it.

All of it lives in a dashboard in your browser at `http://localhost:3000`. It opens by
itself when xPilot starts. Leave the tab open so the sounds can reach you.

## Installing

You need xPilot 4.0 or newer (that is when plugins arrived). Then:

1. Open your xPilot Plugins folder:

   | OS | Path |
   |----|------|
   | Windows | `%LOCALAPPDATA%\org.vatsim.xpilot\Plugins` |
   | macOS | `~/Library/Application Support/org.vatsim.xpilot/Plugins` |
   | Linux | `~/.local/share/org.vatsim.xpilot/Plugins` |

   No Plugins folder yet? Make one with exactly that name.

2. Drop these in:

   ```
   Plugins/SillyxPilot.Plugin.dll
   Plugins/SillyxPilot.Plugin.deps.json
   Plugins/SillyxPilot/wwwroot/...
   ```

3. Start xPilot. Done. It prints the dashboard link in the message area.

One DLL works on all three platforms. No installer, no admin rights, no background
service, nothing to uninstall except deleting the files again.

## Building from source

```
dotnet build SillyxPilot.Plugin/SillyxPilot.Plugin.csproj -c Release
```

That is it. Output lands in `SillyxPilot.Plugin/bin/Release/net8.0/`, laid out exactly
how the Plugins folder wants it. .NET 8 SDK required.

## Why the code looks the way it does

Fair warning before you read the source: xPilot runs plugins on a heavily trimmed .NET
runtime, and finding out what survived the trimming nearly broke me. Things that do not
exist in there, discovered one crash at a time:

* any `async` method at all
* `System.Threading.Thread`
* `string.Split`
* `Socket.Send(byte[])` and `Socket.Receive(byte[])`, but the four argument versions work
* `TrimStart`, while `TrimEnd` is fine, because why not
* number and date formatting like `ToString("0.000")`
* `HttpListener`, WebSockets, `JsonSerializer`, `Process.Start` with a start info, SMTP

So yes, there is a hand written string splitter in here. And a hand written ISO date
formatter. And a web server built on raw sockets with Server-Sent Events instead of
WebSockets. And timers where any sane person would use threads. Every one of those has a
story, and the story ends with "because the normal way crashes xPilot."

The plugin also writes a `diag.txt` on startup that probes the runtime and tests its own
web server. If something breaks on a future xPilot version, that file says exactly what.

## Settings

Open the dashboard, hit Settings:

* which alerts fire for supervisor, ATC and regular private messages
* your Discord webhook for pings
* your network chat username (filtered, keep it clean)
* map traffic range, refresh rate, port, and whether the browser opens on startup

Nothing is preconfigured and your settings never leave your machine.

## Credits

* The [xPilot](https://xpilot.app) team, for the client and the official plugin SDK.
* [VATSIM Radar](https://github.com/VATSIM-Radar/vatsim-radar) for the 777 icon
* The [VAT-Spy Data Project](https://github.com/vatsimnetwork/vatspy-data-project) for
  FIR boundaries and airport data.
* [Leaflet](https://leafletjs.com) for the map.

Not affiliated with VATSIM or the xPilot project. It never touches the network
connection itself, it only listens to what xPilot already tells plugins.

## The point of all this

Stay reachable. Answer your supervisors. And if you cannot be at your keyboard every
second of a long haul, at least have something loud enough to bring you back.

Fly silly, land safe.
