﻿using Newtonsoft.Json;
using StableDiffusionGui.Implementations;
using StableDiffusionGui.Main;
using StableDiffusionGui.MiscUtils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using static StableDiffusionGui.Implementations.InvokeAiMetadata;

namespace StableDiffusionGui.Data
{
    public class ImageMetadata
    {
        public enum MetadataType { InvokeDream, InvokeJson, Auto1111, Nmkdiffusers, Unknown }
        public MetadataType Type { get; set; } = MetadataType.Unknown;
        public string Path { get; set; } = "";
        public string AllText { get; set; } = "";
        public string ParsedText { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string NegativePrompt { get; set; } = "";
        public string CombinedPrompt { get { return InvokeAiUtils.GetCombinedPrompt(Prompt, NegativePrompt); } }
        public int Steps { get; set; } = -1;
        public int BatchSize { get; set; } = 1;
        public Size GeneratedResolution { get; set; } = new Size();
        public float Scale { get; set; } = -1;
        public float ScaleImg { get; set; } = -1;
        public string Sampler { get; set; } = "";
        public long Seed { get; set; } = -1;
        public string InitImgName { get; set; } = "";
        public float InitStrength { get; set; } = 0f;
        public string Model { get; set; } = "";
        public Enums.StableDiffusion.SeamlessMode SeamlessMode { get; set; } = Enums.StableDiffusion.SeamlessMode.Disabled;
        public Enums.Utils.FaceTool FaceTool { get; set; } = (Enums.Utils.FaceTool)(-1);

        private readonly Dictionary<MetadataType, string> _tags = new Dictionary<MetadataType, string>() {
            { MetadataType.InvokeJson, "sd-metadata: " },
            { MetadataType.InvokeDream, "Dream: " },
            { MetadataType.Nmkdiffusers, "Nmkdiffusers:" },
            { MetadataType.Auto1111, "parameters:" },
        };

        public ImageMetadata() { }

        public ImageMetadata(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Logger.Log($"Can't read metadata from invalid path (empty string or missing file): {path}", true);
                return;
            }

            Path = path;

            try
            {
                IEnumerable<MetadataExtractor.Directory> directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(path);
                List<MetadataExtractor.Directory> pngTextDirs = directories.Where(x => x.Name.Lower() == "png-text").ToList();

                if (!pngTextDirs.Any())
                    return;

                var tags = pngTextDirs.SelectMany(textDir => textDir.Tags);
                AllText = string.Join(Environment.NewLine, tags.Select(tag => tag.Description));

                foreach (var tag in tags)
                {
                    if (tag.Description.Contains(_tags[MetadataType.InvokeJson]))
                    {
                        LoadInfoInvokeAiJson(tag.Description.Split(_tags[MetadataType.InvokeJson]).Last());
                        return;
                    }

                    if (tag.Description.Contains(_tags[MetadataType.InvokeDream]) && !tags.Any(t => t.Description.Contains(_tags[MetadataType.InvokeJson])))
                    {
                        LoadInfoInvokeAi(tag.Description.Split(_tags[MetadataType.InvokeDream]).Last());
                        return;
                    }

                    if (tag.Description.Contains(_tags[MetadataType.Auto1111]))
                    {
                        LoadInfoAuto1111(tag.Description.Split(_tags[MetadataType.Auto1111]).Last());
                        return;
                    }

                    if (tag.Description.Contains(_tags[MetadataType.Nmkdiffusers]))
                    {
                        LoadInfoNmkdiffusers(tag.Description.Split(_tags[MetadataType.Nmkdiffusers]).Last());
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load image metadata from '{path}': {ex.Message}\n{ex.StackTrace}", true);
            }
        }

        public void LoadInfoInvokeAiJson (string info)
        {
            Type = MetadataType.InvokeJson;
            ParsedText = info;

            try
            {
                InvokeAiMetadata metadata = info.FromJson<InvokeAiMetadata>(NullValueHandling.Ignore, DefaultValueHandling.Include, true, true);

                Prompt = metadata.ImageData.Prompt.First().Text;

                if (Prompt.EndsWith("]") && Prompt.Contains(" [") && Prompt.Count(x => x == '[') == 1 && Prompt.Count(x => x == ']') == 1)
                {
                    NegativePrompt = Prompt.Split(" [").Last().Split(']')[0];
                    var split = Prompt.Split(" [");
                    Prompt = string.Join(" [", split.Reverse().Skip(1).Reverse());
                }

                Steps = metadata.ImageData.Steps;
                BatchSize = 1;
                GeneratedResolution = new Size(metadata.ImageData.Width, metadata.ImageData.Height);
                Scale = metadata.ImageData.CfgScale;
                Sampler = metadata.ImageData.Sampler;
                Seed = metadata.ImageData.Seed;
                InitStrength = 1f - metadata.ImageData.StrengthSteps;
                InitImgName = "";
                FaceTool = InvokeGetFaceTool(metadata.ImageData.Facetool);
                Model = metadata.ModelId;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load InvokeAI JSON image metadata from: {ex.Message}\n{ex.StackTrace}", true);
            }
        }

        public void LoadInfoInvokeAi(string info)
        {
            Type = MetadataType.InvokeDream;
            ParsedText = info;

            try
            {
                string paramsText = "";

                if (info.Trim().StartsWith("\""))
                {
                    var split = info.Split("\"");
                    Prompt = split[1].Remove("\"").Trim();
                    paramsText = split[2];
                }
                else
                {
                    var split = info.Split(" -");
                    Prompt = split[0];
                    paramsText = string.Join(" -", split.Skip(1));
                }

                if (Prompt.EndsWith("]") && Prompt.Contains(" [") && Prompt.Count(x => x == '[') == 1 && Prompt.Count(x => x == ']') == 1)
                {
                    NegativePrompt = Prompt.Split(" [").Last().Split(']')[0];
                    var split = Prompt.Split(" [");
                    Prompt = string.Join(" [", split.Reverse().Skip(1).Reverse());
                }

                bool oldFormat = !info.Contains("-W ") || !info.Contains("-H "); // Check if metadata uses old format without spaces
                paramsText = " " + paramsText; // Do not remove this.

                var parameters = !oldFormat ? paramsText.Split(" -").Select(x => $"-{x.Trim()}").ToList() : paramsText.Split(" ").Select(x => x.Trim());
                parameters = parameters.Where(s => s.Trim().Length >= 3); // Only valid parameters (3 chars => -, anychar, space)

                if (oldFormat)
                    parameters = parameters.Select(p => p.Insert(2, " ")); // Insert space after arg

                foreach (string s in parameters)
                {
                    string key = s.Split(' ').First().Trim().Remove("-");
                    string value = s.Split(' ').Last().Trim();

                    if (key == "s")
                        Steps = value.GetInt();

                    else if (key == "b")
                        BatchSize = value.GetInt();

                    else if (key == "W")
                        GeneratedResolution = new Size(value.GetInt(), GeneratedResolution.Height);

                    else if (key == "H")
                        GeneratedResolution = new Size(GeneratedResolution.Width, value.GetInt());

                    else if (key == "C")
                        Scale = value.GetFloat();

                    else if (key == "A")
                        Sampler = value.Trim();

                    else if (key == "S")
                        Seed = value.GetLong();

                    else if (key == "f")
                        InitStrength = 1f - value.GetFloat();

                    else if (key == "I")
                        InitImgName = value.Trim();

                    else if (key == "seamless")
                        SeamlessMode = Enums.StableDiffusion.SeamlessMode.SeamlessBoth;

                    else if (key == "ft")
                        FaceTool = InvokeGetFaceTool(value);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load InvokeAI image metadata from: {ex.Message}\n{ex.StackTrace}", true);
            }
        }

        private Enums.Utils.FaceTool InvokeGetFaceTool (string value)
        {
            switch (value.Lower())
            {
                case "gfpgan": return Enums.Utils.FaceTool.Gfpgan;
                case "codeformer": return Enums.Utils.FaceTool.CodeFormer;
                default: return (Enums.Utils.FaceTool)(-1);
            }
        }

        public void LoadInfoAuto1111(string info)
        {
            Type = MetadataType.Auto1111;
            ParsedText = info;

            try
            {
                var lines = info.SplitIntoLines();

                Prompt = lines[0].Trim();
                NegativePrompt = lines[1].Split("Negative prompt: ")[1];
                Steps = lines[2].Split("Steps: ")[1].Split(',')[0].GetInt();
                GeneratedResolution = ParseUtils.GetSize(lines[2].Split("Size: ")[1].Split(',')[0]);
                Scale = lines[2].Split("CFG scale: ")[1].Split(',')[0].GetFloat();
                Sampler = lines[2].Split("Sampler: ")[1].Split(',')[0].Replace(" ", "_");
                Seed = lines[2].Split("Seed: ")[1].Split(',')[0].GetLong();
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load Automatic1111 image metadata from: {ex.Message}\n{ex.StackTrace}", true);
            }
        }

        public void LoadInfoNmkdiffusers(string info)
        {
            ParsedText = info;

            Dictionary<string, string> dict = info.FromJson<Dictionary<string, string>>();

            foreach (var pair in dict)
            {
                switch (pair.Key)
                {
                    case "prompt": Prompt = pair.Value; break;
                    case "promptNeg": NegativePrompt = pair.Value; break;
                    case "initImg": InitImgName = pair.Value; break;
                    case "initStrength": InitStrength = pair.Value.GetFloat(); break;
                    case "steps": Steps = pair.Value.GetInt(); break;
                    case "seed": Seed = pair.Value.GetInt(); break;
                    case "scaleTxt": Scale = pair.Value.GetFloat(); break;
                    case "scaleImg": ScaleImg = pair.Value.GetFloat(); break;
                    case "w": GeneratedResolution = new Size(pair.Value.GetInt(), GeneratedResolution.Height); break;
                    case "h": GeneratedResolution = new Size(GeneratedResolution.Width, pair.Value.GetInt()); break;
                    default: continue;
                }
            }
        }
    }
}
