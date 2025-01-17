using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;
using Serilog.Parsing;
using StarterKit.Framework.Extensions;

namespace StarterKit.Framework.Logging
{
	public class DigipolisFormatter : ITextFormatter
	{
		const string ExtensionPointObsoletionMessage =
			"Extension of JsonFormatter by subclassing is obsolete and will " +
			"be removed in a future Serilog version. Write a custom formatter " +
			"based on JsonValueFormatter instead. See https://github.com/serilog/serilog/pull/819.";

		private static readonly string[] AllowedProperties =
		{
			"CorrelationId",
			"ApplicationId",
			"Request",
			"Response",
			"Host",
			"Headers",
			"Path",
			"Payload",
			"Protocol",
			"Method",
			"Status",
			"Duration",
			"Type",
			"MessageUser",
			"MessageUserIsAuthenticated",
		};

		// Ignore obsoletion errors
#pragma warning disable 618

		readonly bool _omitEnclosingObject;
		readonly string _closingDelimiter;
		readonly bool _renderMessage;
		readonly IFormatProvider _formatProvider;
		readonly IDictionary<Type, Action<object, bool, TextWriter>> _literalWriters;

		/// <summary>
		/// Construct a <see cref="JsonFormatter"/>.
		/// </summary>
		/// <param name="closingDelimiter">A string that will be written after each log event is formatted.
		/// If null, <see cref="Environment.NewLine"/> will be used.</param>
		/// <param name="renderMessage">If true, the message will be rendered and written to the output as a
		/// property named RenderedMessage.</param>
		/// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
		public DigipolisFormatter(
			string closingDelimiter = null,
			bool renderMessage = true,
			IFormatProvider formatProvider = null)
			: this(false, closingDelimiter, renderMessage, formatProvider)
		{
		}

		/// <summary>
		/// Construct a <see cref="JsonFormatter"/>.
		/// </summary>
		/// <param name="omitEnclosingObject">If true, the properties of the event will be written to
		/// the output without enclosing braces. Otherwise, if false, each event will be written as a well-formed
		/// JSON object.</param>
		/// <param name="closingDelimiter">A string that will be written after each log event is formatted.
		/// If null, <see cref="Environment.NewLine"/> will be used. Ignored if <paramref name="omitEnclosingObject"/>
		/// is true.</param>
		/// <param name="renderMessage">If true, the message will be rendered and written to the output as a
		/// property named RenderedMessage.</param>
		/// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
		[Obsolete("The omitEnclosingObject parameter is obsolete and will be removed in a future Serilog version.")]
		public DigipolisFormatter(
			bool omitEnclosingObject,
			string closingDelimiter = null,
			bool renderMessage = true,
			IFormatProvider formatProvider = null)
		{
			_omitEnclosingObject = omitEnclosingObject;
			_closingDelimiter = closingDelimiter ?? Environment.NewLine;
			_renderMessage = renderMessage;
			_formatProvider = formatProvider;

			_literalWriters = new Dictionary<Type, Action<object, bool, TextWriter>>
			{
				{ typeof(bool), (v, q, w) => WriteBoolean((bool)v, w) },
				{ typeof(char), (v, q, w) => WriteString(((char)v).ToString(), w) },
				{ typeof(byte), WriteToString },
				{ typeof(sbyte), WriteToString },
				{ typeof(short), WriteToString },
				{ typeof(ushort), WriteToString },
				{ typeof(int), WriteToString },
				{ typeof(uint), WriteToString },
				{ typeof(long), WriteToString },
				{ typeof(ulong), WriteToString },
				{ typeof(float), (v, q, w) => WriteSingle((float)v, w) },
				{ typeof(double), (v, q, w) => WriteDouble((double)v, w) },
				{ typeof(decimal), WriteToString },
				{ typeof(string), (v, q, w) => WriteString((string)v, w) },
				{ typeof(DateTime), (v, q, w) => WriteDateTime((DateTime)v, w) },
				{ typeof(DateTimeOffset), (v, q, w) => WriteOffset((DateTimeOffset)v, w) },
				{ typeof(ScalarValue), (v, q, w) => WriteLiteral(((ScalarValue)v).Value, w, q) },
				{ typeof(SequenceValue), (v, q, w) => WriteSequence(((SequenceValue)v).Elements, w) },
				{ typeof(DictionaryValue), (v, q, w) => WriteDictionary(((DictionaryValue)v).Elements, w) },
				{
					typeof(StructureValue),
					(v, q, w) => WriteStructure(((StructureValue)v).TypeTag, ((StructureValue)v).Properties, w)
				},
			};
		}

		/// <summary>
		/// filter out properties that we don't want
		/// Do include the properties used to create the messageTemplate as we do want to keep those
		/// </summary>
		/// <param name="properties"></param>
		/// <returns></returns>
		private IReadOnlyDictionary<string, LogEventPropertyValue> GetFilteredProperties(
			IReadOnlyDictionary<string, LogEventPropertyValue> properties)
		{
			return properties
				.Where(p => AllowedProperties.Contains(p.Key))
				.ToDictionary(
					k => k.Key,
					v => v.Value);
		}

		private IReadOnlyDictionary<string, LogEventPropertyValue> GetMessageProperties(
			IReadOnlyDictionary<string, LogEventPropertyValue> properties, IEnumerable<MessageTemplateToken> tokens)
		{
			var propertiesAllowed =
				tokens.OfType<PropertyToken>()
					.Select(t => t.PropertyName);

			return properties
				.Where(p => propertiesAllowed.Contains(p.Key))
				.ToDictionary(
					k => k.Key,
					v => v.Value);
		}

		/// <summary>
		/// Format the log event into the output.
		/// </summary>
		/// <param name="logEvent">The event to format.</param>
		/// <param name="output">The output.</param>
		/// <exception cref="ArgumentNullException">When <paramref name="logEvent"/> is <code>null</code></exception>
		/// <exception cref="ArgumentNullException">When <paramref name="output"/> is <code>null</code></exception>
		public void Format(LogEvent logEvent, TextWriter output)
		{
			if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
			if (output == null) throw new ArgumentNullException(nameof(output));

			if (!_omitEnclosingObject)
				output.Write("{");

			var delim = "";
			WriteTimestamp(logEvent.Timestamp, ref delim, output);
			WriteLevel(logEvent.Level, ref delim, output);

			// if it is an exception or a database log we need to keep a small message template text, else render
			if (logEvent.Exception != null)
			{
				WriteMessageTemplate(logEvent.MessageTemplate.Text, ref delim, output);
				WriteException(logEvent.Exception, ref delim, output);
			}
			else if (logEvent.MessageTemplate.Text.StartsWith("Failed executing DbCommand"))
			{
				WriteRenderedMessage("Failed executing DbCommand", ref delim, output);
			}
			else
			{
				var message = logEvent.RenderMessage(_formatProvider);
				WriteRenderedMessage(message, ref delim, output);
			}

			//now write all message properties as a messageProperties object
			var messageProperties = GetMessageProperties(logEvent.Properties, logEvent.MessageTemplate.Tokens);
			if (messageProperties.Count != 0)
				WriteMessageProperties(messageProperties, output);

			var properties = GetFilteredProperties(logEvent.Properties);
			if (properties.Count != 0)
				WriteProperties(properties, output);

			if (!_omitEnclosingObject)
			{
				output.Write("}");
				output.Write(_closingDelimiter);
			}
		}

		/// <summary>
		/// Adds a writer function for a given type.
		/// </summary>
		/// <param name="type">The type of values, which <paramref name="writer" /> handles.</param>
		/// <param name="writer">The function, which writes the values.</param>
		/// <exception cref="ArgumentNullException">When <paramref name="type"/> is <code>null</code></exception>
		/// <exception cref="ArgumentNullException">When <paramref name="writer"/> is <code>null</code></exception>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected void AddLiteralWriter(Type type, Action<object, TextWriter> writer)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));
			if (writer == null) throw new ArgumentNullException(nameof(writer));

			_literalWriters[type] = (v, _, w) => writer(v, w);
		}

		/// <summary>
		/// Writes out individual renderings of attached properties
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteRenderings(IGrouping<string, PropertyToken>[] tokensWithFormat,
			IReadOnlyDictionary<string, LogEventPropertyValue> properties, TextWriter output)
		{
			output.Write(",\"{0}\":{{", FormatProperty("Renderings"));
			WriteRenderingsValues(tokensWithFormat, properties, output);
			output.Write("}");
		}

		/// <summary>
		/// Writes out the values of individual renderings of attached properties
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteRenderingsValues(IGrouping<string, PropertyToken>[] tokensWithFormat,
			IReadOnlyDictionary<string, LogEventPropertyValue> properties, TextWriter output)
		{
			var rdelim = "";
			foreach (var ptoken in tokensWithFormat)
			{
				output.Write(rdelim);
				rdelim = ",";
				output.Write("\"");
				output.Write(ptoken.Key);
				output.Write("\":[");

				var fdelim = "";
				foreach (var format in ptoken)
				{
					output.Write(fdelim);
					fdelim = ",";

					output.Write("{");
					var eldelim = "";

					WriteJsonProperty("Format", format.Format, ref eldelim, output);

					var sw = new StringWriter();
					// MessageTemplateRenderer.RenderPropertyToken(format, properties, sw, _formatProvider, isLiteral: true, isJson: false);
					WriteJsonProperty("Rendering", sw.ToString(), ref eldelim, output);

					output.Write("}");
				}

				output.Write("]");
			}
		}

		/// <summary>
		/// Writes all message template specific properties to a separate object
		/// </summary>
		protected virtual void WriteMessageProperties(
			IReadOnlyDictionary<string, LogEventPropertyValue> properties,
			TextWriter output)
		{
			output.Write(",\"{0}\":{{", "messageProperties");
			WritePropertiesValues(properties, output);
			output.Write("}");
		}

		/// <summary>
		/// Writes out the attached properties
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties,
			TextWriter output)
		{
			output.Write(",");
			WritePropertiesValues(properties, output);
			//output.Write("}");
		}

		/// <summary>
		/// Writes out the attached properties values
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WritePropertiesValues(IReadOnlyDictionary<string, LogEventPropertyValue> properties,
			TextWriter output)
		{
			var precedingDelimiter = "";
			foreach (var property in properties)
			{
				WriteJsonProperty(property.Key, property.Value, ref precedingDelimiter, output);
			}
		}

		/// <summary>
		/// Writes out the attached exception
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteException(Exception exception, ref string delim, TextWriter output)
		{
			WriteJsonProperty("Exception", exception, ref delim, output);
		}

		/// <summary>
		/// (Optionally) writes out the rendered message
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteRenderedMessage(string message, ref string delim, TextWriter output)
		{
			WriteJsonProperty("Message", message, ref delim, output);
		}

		/// <summary>
		/// Writes out the message template for the logevent.
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteMessageTemplate(string template, ref string delim, TextWriter output)
		{
			WriteJsonProperty("Message", template, ref delim, output);
		}

		/// <summary>
		/// Translate log level according to Digipolis logging guidelines
		/// </summary>
		/// <param name="level"></param>
		/// <returns></returns>
		protected virtual string TranslateLevel(LogEventLevel level)
		{
			switch (level)
			{
				case LogEventLevel.Information:
					return "INFO";
				case LogEventLevel.Warning:
					return "WARN";
				default:
					return level.ToString().ToUpper();
			}
		}

		/// <summary>
		/// Writes out the log level
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteLevel(LogEventLevel level, ref string delim, TextWriter output)
		{
			WriteJsonProperty("Level", TranslateLevel(level), ref delim, output);
		}

		/// <summary>
		/// Writes out the log timestamp
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteTimestamp(DateTimeOffset timestamp, ref string delim, TextWriter output)
		{
			WriteJsonProperty("Timestamp", timestamp, ref delim, output);
		}

		/// <summary>
		/// Writes out a structure property
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteStructure(string typeTag, IEnumerable<LogEventProperty> properties,
			TextWriter output)
		{
			output.Write("{");

			var delim = "";
			if (typeTag != null)
				WriteJsonProperty("_typeTag", typeTag, ref delim, output);

			foreach (var property in properties)
				WriteJsonProperty(property.Name, property.Value, ref delim, output);

			output.Write("}");
		}

		/// <summary>
		/// Writes out a sequence property
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteSequence(IEnumerable elements, TextWriter output)
		{
			output.Write("[");
			var delim = "";
			foreach (var value in elements)
			{
				output.Write(delim);
				delim = ",";
				WriteLiteral(value, output);
			}

			output.Write("]");
		}

		/// <summary>
		/// Writes out a dictionary
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteDictionary(IReadOnlyDictionary<ScalarValue, LogEventPropertyValue> elements,
			TextWriter output)
		{
			output.Write("{");
			var delim = "";
			foreach (var element in elements)
			{
				output.Write(delim);
				delim = ",";
				WriteLiteral(element.Key, output, forceQuotation: true);
				output.Write(":");
				WriteLiteral(element.Value, output);
			}

			output.Write("}");
		}

		/// <summary>
		/// Writes out a json property with the specified value on output writer
		/// </summary>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteJsonProperty(string name, object value, ref string precedingDelimiter,
			TextWriter output)
		{
			output.Write(precedingDelimiter);
			output.Write("\"");
			output.Write(FormatProperty(name));
			output.Write("\":");
			WriteLiteral(value, output);
			precedingDelimiter = ",";
		}

		/// <summary>
		/// Allows a subclass to write out objects that have no configured literal writer.
		/// </summary>
		/// <param name="value">The value to be written as a json construct</param>
		/// <param name="output">The writer to write on</param>
		[Obsolete(ExtensionPointObsoletionMessage)]
		protected virtual void WriteLiteralValue(object value, TextWriter output)
		{
			WriteString(value.ToString() ?? "", output);
		}

		void WriteLiteral(object value, TextWriter output, bool forceQuotation = false)
		{
			if (value == null)
			{
				output.Write("null");
				return;
			}

			if (_literalWriters.TryGetValue(value.GetType(), out var writer))
			{
				writer(value, forceQuotation, output);
				return;
			}

			WriteLiteralValue(value, output);
		}

		static void WriteToString(object number, bool quote, TextWriter output)
		{
			if (quote) output.Write('"');

			if (number is IFormattable fmt)
				output.Write(fmt.ToString(null, CultureInfo.InvariantCulture));
			else
				output.Write(number.ToString());

			if (quote) output.Write('"');
		}

		static void WriteBoolean(bool value, TextWriter output)
		{
			output.Write(value ? "true" : "false");
		}

		static void WriteSingle(float value, TextWriter output)
		{
			output.Write(value.ToString("R", CultureInfo.InvariantCulture));
		}

		static void WriteDouble(double value, TextWriter output)
		{
			output.Write(value.ToString("R", CultureInfo.InvariantCulture));
		}

		static void WriteOffset(DateTimeOffset value, TextWriter output)
		{
			output.Write("\"");
			output.Write(value.ToString("o"));
			output.Write("\"");
		}

		static void WriteDateTime(DateTime value, TextWriter output)
		{
			output.Write("\"");
			output.Write(value.ToString("o"));
			output.Write("\"");
		}

		static void WriteString(string value, TextWriter output)
		{
			JsonValueFormatter.WriteQuotedJsonString(value, output);
		}

		static string FormatProperty(string property)
		{
			return property.ToCamelCase();
		}

		/// <summary>
		/// Perform simple JSON string escaping on <paramref name="s"/>.
		/// </summary>
		/// <param name="s">A raw string.</param>
		/// <returns>A JSON-escaped version of <paramref name="s"/>.</returns>
		[Obsolete("Use JsonValueFormatter.WriteQuotedJsonString() instead."),
		 EditorBrowsable(EditorBrowsableState.Never)]
		public static string Escape(string s)
		{
			if (s == null) return null;

			var escapedResult = new StringWriter();
			JsonValueFormatter.WriteQuotedJsonString(s, escapedResult);
			var quoted = escapedResult.ToString();
			return quoted.Substring(1, quoted.Length - 2);
		}
	}
}