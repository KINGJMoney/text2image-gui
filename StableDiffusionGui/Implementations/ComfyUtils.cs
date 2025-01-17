﻿using StableDiffusionGui.Data;
using StableDiffusionGui.Io;
using StableDiffusionGui.Main;
using StableDiffusionGui.MiscUtils;
using StableDiffusionGui.Ui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static StableDiffusionGui.Implementations.ComfyData;
using static StableDiffusionGui.Main.Enums.StableDiffusion;

namespace StableDiffusionGui.Implementations
{
    public class ComfyUtils
    {
        public static string GetVramArg()
        {
            var preset = ParseUtils.GetEnum<Enums.Comfy.VramPreset>(Config.Instance.ComfyVramPreset.ToString(), true, Strings.ComfyVramPresets);
            if (preset == Enums.Comfy.VramPreset.GpuOnly) return "gpu-only";
            if (preset == Enums.Comfy.VramPreset.HighVram) return "highvram";
            if (preset == Enums.Comfy.VramPreset.NormalVram) return "normalvram";
            if (preset == Enums.Comfy.VramPreset.LowVram) return "lowvram";
            if (preset == Enums.Comfy.VramPreset.NoVram) return "novram";
            return "";
        }

        public static string ControlnetCompatCheck(List<ControlnetInfo> cnets, ModelArch modelArch)
        {
            ModelArch cnetArch = (ModelArch)(-1);
            bool compat = true;

            foreach (ControlnetInfo info in cnets)
            {
                if (info.Model.Lower().Contains("sd14") || info.Model.Lower().Contains("sd15"))
                {
                    cnetArch = ModelArch.Sd1;
                    compat = new[] { ModelArch.Sd1, ModelArch.Sd1Inpaint }.Contains(modelArch);
                }

                else if (info.Model.Lower().Contains("xl"))
                {
                    cnetArch = ModelArch.SdXlBase;
                    compat = new[] { ModelArch.SdXlBase, ModelArch.SdXlRefine }.Contains(modelArch);
                }

                else if (info.Model.Lower().Contains("sd2"))
                {
                    cnetArch = ModelArch.Sd2;
                    compat = new[] { ModelArch.Sd2, ModelArch.Sd2Inpaint, ModelArch.Sd2V }.Contains(modelArch);
                }

                if (!compat)
                {
                    string mdlArchStr = Strings.ModelArch.Get(modelArch.ToString());
                    string cnetArchStr = Strings.ModelArch.Get(cnetArch.ToString());

                    if (cnetArchStr.IsEmpty())
                        cnetArchStr = "Unknown";
                    else
                        cnetArchStr += " (Assumed; based on filename)";

                    return $"One or more enabled ControlNet models are incompatible with your current Stable Diffusion model.\n\nModel Architecture:\n{mdlArchStr}\n\nControlNet Architecture:\n{cnetArchStr}";
                }
            }

            return ""; // Fallback if arch was not detected. Will error if incompatible, but maybe the author just didn't name the file properly
        }

        private static readonly Regex _invokeEmbeddingPattern = new Regex(@"<([^>]+)>", RegexOptions.Compiled);

        public static string SanitizePrompt (string prompt)
        {
            prompt = _invokeEmbeddingPattern.Replace(prompt, "embedding:$1"); // Change <filename> to embedding:filename

            return prompt;
        }
    }
}
