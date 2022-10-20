using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace KH2BattleVoiceRandomizer
{
	class Program
	{
		const string SCDMagic = "SEDBSSCF";

		[STAThread]
		static void Main(string[] args)
		{
			List<SCDFile> files = new List<SCDFile>();
			Settings settings;
			if (File.Exists("settings.yml"))
				settings = new Deserializer().Deserialize<Settings>(File.ReadAllText("settings.yml"));
			else
				settings = new Settings();
			if (settings.KH2Folder == null)
			{
				using (var dlg = new Ookii.Dialogs.WinForms.VistaFolderBrowserDialog() { Description = "Select your extracted KH2 folder.", UseDescriptionForTitle = true, SelectedPath = Directory.GetCurrentDirectory() })
				{
					if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						settings.KH2Folder = dlg.SelectedPath;
						File.WriteAllText("settings.yml", new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build().Serialize(settings));
					}
					else
						return;
				}
			}
			Mod mod = new Mod
			{
				Title = "Battle Voice Randomizer",
				Description = "Randomizes voices in battle.",
				Assets = new List<Asset>()
			};
			foreach (string fn in Directory.EnumerateFiles(Path.Combine(settings.KH2Folder, $@"voice\{settings.Language}\battle"), "*.scd"))
				files.Add(new SCDFile(fn, settings.KH2Folder.Length + 1));
			Random rand = new Random();
			var voices = files.SelectMany(a => a.Voices.Where(b => !b.IsDummy)).ToArray();
			int[] keys = new int[voices.Length];
			for (int i = 0; i < voices.Length; i++)
				keys[i] = rand.Next();
			Array.Sort(keys, voices);
			int vi = 0;
			foreach (var file in files)
			{
				for (int i = 0; i < file.Voices.Count; i++)
					if (!file.Voices[i].IsDummy)
						file.Voices[i] = voices[vi++];
				Directory.CreateDirectory(Path.GetDirectoryName(file.Name));
				file.Save();
				string name = file.Name.Replace('\\', '/');
				Asset src = new Asset() { Name = name };
				Asset asset = new Asset() { Name = name, Method = "copy", Sources = new List<Asset>() { src } };
				mod.Assets.Add(asset);
			}
			files.Clear();
			foreach (string dir in Directory.EnumerateDirectories(Path.Combine(settings.KH2Folder, @"remastered\obj"), $"*.a.{settings.Language}"))
			{
				string d2 = Path.Combine(dir, $@"voice\battle\{settings.Language}");
				if (Directory.Exists(d2))
					foreach (string fn in Directory.EnumerateFiles(d2))
					{
						using (FileStream fs = File.OpenRead(fn))
						{
							byte[] buf = new byte[SCDMagic.Length];
							fs.Read(buf, 0, buf.Length);
							if (!System.Text.Encoding.ASCII.GetString(buf).Equals(SCDMagic, StringComparison.Ordinal))
								continue;
						}
						files.Add(new SCDFile(fn, settings.KH2Folder.Length + 1));
					}
			}
			voices = files.SelectMany(a => a.Voices.Where(b => !b.IsDummy)).ToArray();
			keys = new int[voices.Length];
			for (int i = 0; i < voices.Length; i++)
				keys[i] = rand.Next();
			Array.Sort(keys, voices);
			vi = 0;
			foreach (var file in files)
			{
				for (int i = 0; i < file.Voices.Count; i++)
					if (!file.Voices[i].IsDummy)
						file.Voices[i] = voices[vi++];
				Directory.CreateDirectory(Path.GetDirectoryName(file.Name));
				file.Save();
				string name = file.Name.Replace('\\', '/');
				Asset src = new Asset() { Name = name };
				Asset asset = new Asset() { Name = name, Method = "copy", Sources = new List<Asset>() { src } };
				mod.Assets.Add(asset);
			}
			File.WriteAllText("mod.yml", new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build().Serialize(mod));
		}
	}

	class Settings
	{
		public string KH2Folder { get; set; }
		public string Language { get; set; } = "us";
	}

	class Mod
	{
		[YamlMember(Alias = "title")]
		public string Title { get; set; }
		[YamlMember(Alias = "description")]
		public string Description { get; set; }
		[YamlMember(Alias = "assets")]
		public List<Asset> Assets { get; set; } = new List<Asset>();
	}

	class Asset
	{
		[YamlMember(Alias = "name")]
		public string Name { get; set; }
		[YamlMember(Alias = "type")]
		public string Type { get; set; }
		[YamlMember(Alias = "method")]
		public string Method { get; set; }
		[YamlMember(Alias = "source")]
		public List<Asset> Sources { get; set; }
	}

	class SCDFile
	{
		public string Name { get; }
		readonly byte[] data;
		readonly int headersize;
		readonly int table3off;
		readonly List<int> offoffs = new List<int>();
		readonly Dictionary<int, int> lengthoffs = new Dictionary<int, int>();
		public List<Voice> Voices { get; } = new List<Voice>();

		public SCDFile(string filename, int baselength)
		{
			Name = filename.Substring(baselength);
			data = File.ReadAllBytes(filename);
			ushort headoff = BitConverter.ToUInt16(data, 0xE);
			int[] table1 = new int[BitConverter.ToUInt16(data, headoff)];
			int[] table2 = new int[BitConverter.ToUInt16(data, headoff + 2)];
			int[] table3 = new int[BitConverter.ToUInt16(data, headoff + 4)];
			int table1off = headoff + 0x20;
			int table2off = BitConverter.ToInt32(data, headoff + 8);
			table3off = BitConverter.ToInt32(data, headoff + 12);
			for (int i = 0; i < table1.Length; i++)
				table1[i] = BitConverter.ToInt32(data, table1off + (i * 4));
			for (int i = 0; i < table2.Length; i++)
				table2[i] = BitConverter.ToInt32(data, table2off + (i * 4));
			for (int i = 0; i < table3.Length; i++)
				table3[i] = BitConverter.ToInt32(data, table3off + (i * 4));
			int[] table3sort = new int[table3.Length + 1];
			table3.CopyTo(table3sort, 0);
			table3sort[table3.Length] = data.Length;
			Array.Sort(table3sort);
			headersize = table3sort[0];
			for (int i = 0; i < table3.Length; i++)
			{
				offoffs.Add(table3off + (i * 4));
				int size = table3sort[Array.IndexOf(table3sort, table3[i]) + 1] - table3[i];
				byte[] vc = new byte[size];
				Array.Copy(data, table3[i], vc, 0, size);
				Voices.Add(new Voice(vc));
			}
			if (BitConverter.ToUInt32(data, 8) <= 3)
				for (int i = 0; i < table1.Length; i++)
					if (BitConverter.ToUInt16(data, table1[i]) != 0x100)
					{
						int off = table2[BitConverter.ToUInt16(data, table1[i] + 0x10)] + 0x50;
						ushort wavind = BitConverter.ToUInt16(data, table1[i] + 0x12);
						lengthoffs[wavind] = off;
						Voices[wavind].Length = BitConverter.ToUInt32(data, off);
					}
		}

		public void Save()
		{
			byte[] newdata = new byte[headersize + Voices.Sum(a => a.Data.Length)];
			Array.Copy(data, newdata, headersize);
			int off = headersize;
			for (int i = 0; i < Voices.Count; i++)
			{
				BitConverter.GetBytes(off).CopyTo(newdata, offoffs[i]);
				Voices[i].Data.CopyTo(newdata, off);
				if (!Voices[i].IsDummy && BitConverter.ToUInt32(newdata, 8) <= 3)
					BitConverter.GetBytes(Voices[i].Length).CopyTo(newdata, lengthoffs[i]);
				off += Voices[i].Data.Length;
			}
			BitConverter.GetBytes(off).CopyTo(newdata, 0x10);
			File.WriteAllBytes(Name, newdata);
		}
	}

	class Voice
	{
		public byte[] Data { get; }
		public bool IsDummy { get; }
		public uint Length { get; set; }

		public Voice(byte[] data)
		{
			Data = data;
			IsDummy = BitConverter.ToInt32(data, 0xC) == -1;
		}
	}
}
