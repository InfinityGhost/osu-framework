// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
#if NET5_0
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Tablet;
using osu.Framework.Logging;

#endif

namespace osu.Framework.Input.Handlers.Tablet
{
#if NET5_0
    public class FrameworkTabletDriver : Driver
    {
        public FrameworkTabletDriver()
        {
            Log.Output += (sender, logMessage) => Logger.Log($"{logMessage.Group}: {logMessage.Message}");
            DevicesChanged += (sender, args) =>
            {
                if (Tablet == null && args.Additions.Any())
                    DetectTablet();
            };
        }

        public void DetectTablet()
        {
            foreach (var config in getConfigurations())
            {
                if (TryMatch(config))
                    break;
            }
        }

        private IEnumerable<TabletConfiguration> getConfigurations()
        {
            // Retrieve all embedded configurations
            var asm = typeof(Driver).Assembly;
            return asm.GetManifestResourceNames()
                      .Where(path => path.Contains(".json"))
                      .Select(path => deserialize(asm.GetManifestResourceStream(path)));
        }

        private TabletConfiguration deserialize(Stream stream)
        {
            using (var tr = new StreamReader(stream))
            using (var jr = new JsonTextReader(tr))
                return configurationSerializer.Deserialize<TabletConfiguration>(jr);
        }

        private JsonSerializer configurationSerializer { get; } = new JsonSerializer();
    }
#endif
}