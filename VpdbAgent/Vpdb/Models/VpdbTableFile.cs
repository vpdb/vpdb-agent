﻿using Newtonsoft.Json;
using System;
using System.Linq;
using System.Runtime.Serialization;
using LiteDB;
using ReactiveUI;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbTableFile : ReactiveObject
	{
		private VpdbImage _thumb;

		[DataMember] public DateTime ReleasedAt { get; set; }
		[DataMember] public VpdbFlavor Flavor { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] public VpdbFile PlayfieldImage { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] public VpdbFile PlayfieldVideo { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] [JsonProperty(PropertyName = "file")] public VpdbFile Reference { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableBuilds)] public ReactiveList<VpdbCompatibility> Compatibility;
		[DataMember] public VpdbImage Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }

		public override string ToString()
		{
			return $"{Flavor.Lighting}/{Flavor.Orientation} ({string.Join(",", Compatibility.Select(c => c.Label))})";
		}

		public class VpdbCompatibility
		{
			[BsonId] public string Id { get; set; }
			public string Label { get; set; }
			public VpdbPlatform Platform { get; set; }
			public string MajorVersion { get; set; }
			public string DownloadUrl { get; set; }
			public DateTime BuiltAt { get; set; }
			public bool IsRange { get; set; }

			public override string ToString()
			{
				return $"{Label} ({Platform})";
			}
		}

		/// <summary>
		/// Platform as defined in the file's build at VPDB.
		/// </summary>
		public enum VpdbPlatform
		{
			/// <summary>
			/// Visual Pinball
			/// </summary>
			VP,

			/// <summary>
			/// Future Pinball
			/// </summary>
			FP,

			/// <summary>
			/// For testing only
			/// </summary>
			[Obsolete("Only use for testing!")]
			Unknown 
		}
	}
}
