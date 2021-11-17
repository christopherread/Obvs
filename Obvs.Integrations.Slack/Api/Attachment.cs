using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class Attachment
	{
		[DataMember(Name = "fallback")]
		public string Fallback { get; set; }

		[DataMember(Name = "color")]
		public string Colour { get; set; }

		[DataMember(Name = "pretext")]
		public string Pretext { get; set; }

		[DataMember(Name = "author_name")]
		public string AuthorName { get; set; }

		[DataMember(Name = "author_link")]
		public string AuthorLink { get; set; }

		[DataMember(Name = "author_icon")]
		public string AuthorIcon { get; set; }

		[DataMember(Name = "title")]
		public string Title { get; set; }

		[DataMember(Name = "title_link")]
		public string TitleLink { get; set; }

		[DataMember(Name = "text")]
		public string Text { get; set; }

		[DataMember(Name = "fields")]
		public Field[] Fields { get; set; }

		[DataMember(Name = "image_url")]
		public string ImageUrl { get; set; }

		[DataMember(Name = "thumb_url")]
		public string ThumbUrl { get; set; }
	}

	[DataContract]
	public class Field
	{
		[DataMember(Name = "title")]
		public string Title { get; set; }

		[DataMember(Name = "value")]
		public string Value { get; set; }

		[DataMember(Name = "short")]
		public bool Short { get; set; }

		public Field(string title, string value, bool isShort = true)
		{
			Title = title;
			Value = value;
			Short = isShort;
		}
	}
}
