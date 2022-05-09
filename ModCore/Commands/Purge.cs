﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using ModCore.Entities;
using ModCore.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ModCore.Logic.Extensions;

namespace ModCore.Commands
{
	[Group("purge"), Aliases("p"), RequirePermissions(Permissions.ManageMessages), CheckDisable]
	public class Purge : BaseCommandModule
	{
		private static readonly Regex SpaceReplacer = new Regex(" {2,}", RegexOptions.Compiled);

		[GroupCommand, Description("Delete an amount of messages from the current channel.")]
		public async Task ExecuteGroupAsync(CommandContext ctx, [Description("Amount of messages to remove (max 100)")]int limit = 50,
			[Description("Amount of messages to skip")]int skip = 0)
		{
			var i = 0;
			var messages = await ctx.Channel.GetMessagesBeforeAsync(ctx.Message.Id, limit);
			var delete = new List<DiscordMessage>();
			foreach (var message in messages)
			{
				if (i < skip)
					i++;
				else
					delete.Add(message);
			}
			if (delete.Any())
				await ctx.Channel.DeleteMessagesAsync(delete, "Purged messages.");
			var resp = await ctx.SafeRespondUnformattedAsync("✅ Latest messages deleted.");
			await Task.Delay(2000);
			await resp.DeleteAsync("Purge command executed.");
			await ctx.Message.DeleteAsync("Purge command executed.");

			await ctx.LogActionAsync($"Purged messages.\nChannel: #{ctx.Channel.Name} ({ctx.Channel.Id})");
		}

		[Command("user"), Description("Delete an amount of messages by an user."), Aliases("u", "pu"), CheckDisable]
		public async Task PurgeUserAsync(CommandContext context, [Description("User to delete messages from")]DiscordUser user,
		[Description("Amount of messages to remove (max 100)")]int limit = 50, [Description("Amount of messages to skip")]int skip = 0)
		{
			var i = 0;
			var messages = await context.Channel.GetMessagesBeforeAsync(context.Message.Id, limit);
			var deletThis = new List<DiscordMessage>();
			foreach (var m in messages)
			{
				if (user != null && m.Author.Id != user.Id) 
					continue;

				if (i < skip)
					i++;
				else
					deletThis.Add(m);
			}
			if (deletThis.Any())
				await context.Channel.DeleteMessagesAsync(deletThis,
					$"Purged messages by {user?.Username}#{user?.Discriminator} (ID:{user?.Id})");
			var response = await context.SafeRespondAsync($"✅ Latest messages by {user?.Mention} (ID:{user?.Id}) deleted.");
			await Task.Delay(2000);
			await response.DeleteAsync("Purge command executed.");
			await context.Message.DeleteAsync("Purge command executed.");

			await context.LogActionAsync(
				$"Purged messages.\nUser: {user?.Username}#{user?.Discriminator} (ID:{user?.Id})\nChannel: #{context.Channel.Name} ({context.Channel.Id})");
		}

		[Command("regexp"), Description(
		 "For power users! Delete messages from the current channel by regular expression match. " +
		 "Pass a Regexp in ECMAScript ( /expression/flags ) format, or simply a regex string " +
		 "in quotes."), Aliases("purgeregex", "pr", "r"), CheckDisable]
		public async Task PurgeRegexpAsync(CommandContext context, [Description("Your regex")] string regex,
		[Description("Amount of messages to remove (max 100)")]int limit = 50, [Description("Amount of messages to skip")]int skip = 0)
		{
			// TODO add a flag to disable CultureInvariant.
			var regexOptions = RegexOptions.CultureInvariant;
			// kept here for displaying in the result
			var flags = "";

			if (string.IsNullOrEmpty(regex))
			{
				await context.SafeRespondUnformattedAsync("⚠️ Regex is empty");
				return;
			}
			var blockType = regex[0];
			if (blockType == '"' || blockType == '/')
			{
				// token structure
				// "regexp" limit? skip?
				// /regexp/ limit? skip?
				// /regexp/ flags limit? skip? 
				var tokens = Tokenize(SpaceReplacer.Replace(regex, " ").Trim(), ' ', blockType);
				regex = tokens[0];
				if (tokens.Count > 1)
				{
					// parse flags only in ECMAScript regexp literal
					if (blockType == '/')
					{
						// if tokens[1] is a valid integer then it's `limit`. otherwise it's `flags`, and we remove it
						// for the other bits.
						flags = tokens[1];
						if (!int.TryParse(flags, out var _))
						{
							// remove the flags element
							tokens.RemoveAt(1);

							if (flags.Contains('m'))
							{
								regexOptions |= RegexOptions.Multiline;
							}
							if (flags.Contains('i'))
							{
								regexOptions |= RegexOptions.IgnoreCase;
							}
							if (flags.Contains('s'))
							{
								regexOptions |= RegexOptions.Singleline;
							}
							if (flags.Contains('x'))
							{
								regexOptions |= RegexOptions.ExplicitCapture;
							}
							if (flags.Contains('r'))
							{
								regexOptions |= RegexOptions.RightToLeft;
							}
							// for debugging only
							if (flags.Contains('c'))
							{
								regexOptions |= RegexOptions.Compiled;
							}
						}
					}

					if (int.TryParse(tokens[1], out var result))
					{
						limit = result;
					}
					else
					{
						await context.SafeRespondUnformattedAsync("⚠️ " + tokens[1] + " is not a valid int");
						return;
					}
					if (tokens.Count > 2)
					{
						if (int.TryParse(tokens[2], out var res2))
						{
							skip = res2;
						}
						else
						{
							await context.SafeRespondUnformattedAsync("⚠️" + tokens[2] + " is not a valid int");
							return;
						}
					}
				}
			}
			var regexCompiled = new Regex(regex, regexOptions);

			var i = 0;
			var messages = await context.Channel.GetMessagesBeforeAsync(context.Message.Id, limit);
			var deletThis = new List<DiscordMessage>();
			foreach (var message in messages)
			{
				if (!regexCompiled.IsMatch(message.Content)) continue;

				if (i < skip)
					i++;
				else
					deletThis.Add(message);
			}
			var resultString =
				$"✅ Purged {deletThis.Count} messages by /{regex.Replace("/", @"\/").Replace(@"\", @"\\")}/{flags}";
			if (deletThis.Any())
				await context.Channel.DeleteMessagesAsync(deletThis, resultString);
			var response = await context.SafeRespondUnformattedAsync(resultString);
			await Task.Delay(2000);
			await response.DeleteAsync("Purge command executed.");
			await context.Message.DeleteAsync("Purge command executed.");

			await context.LogActionAsync(
				$"Purged {deletThis.Count} messages.\nRegex: ```\n{regex}```\nFlags: {flags}\nChannel: #{context.Channel.Name} ({context.Channel.Id})");
		}

		[Command("commands"), Description("Purge ModCore's messages."), Aliases("c", "self", "own"),
	 RequirePermissions(Permissions.ManageMessages), CheckDisable]
		public async Task CleanAsync(CommandContext context)
		{
			var guildsettings = context.GetGuildSettings() ?? new GuildSettings();
			var prefix = guildsettings?.Prefix ?? "?>";
			var messages = await context.Channel.GetMessagesBeforeAsync(context.Message.Id, 100);
			var deletThis = messages.Where(m => m.Author.Id == context.Client.CurrentUser.Id || m.Content.StartsWith(prefix))
				.ToList();
			if (deletThis.Any())
				await context.Channel.DeleteMessagesAsync(deletThis, "Cleaned up commands");
			var response = await context.SafeRespondUnformattedAsync("✅ Latest messages deleted.");
			await Task.Delay(2000);
			await response.DeleteAsync("Clean command executed.");
			await context.Message.DeleteAsync("Clean command executed.");

			await context.LogActionAsync();
		}

		[Command("bots"), Description("Purge messages from all bots in this channel"), Aliases("b", "bot"),
	 RequirePermissions(Permissions.ManageMessages), CheckDisable]
		public async Task PurgeBotsAsync(CommandContext context)
		{
			var guildsettings = context.GetGuildSettings() ?? new GuildSettings();
			var prefix = guildsettings?.Prefix ?? "?>";
			var messages = await context.Channel.GetMessagesBeforeAsync(context.Message.Id, 100);
			var deletThis = messages.Where(m => m.Author.IsBot || m.Content.StartsWith(prefix))
				.ToList();
			if (deletThis.Any())
				await context.Channel.DeleteMessagesAsync(deletThis, "Cleaned up commands");
			var response = await context.SafeRespondUnformattedAsync("✅ Latest messages deleted.");
			await Task.Delay(2000);
			await response.DeleteAsync("Purge bot command executed.");
			await context.Message.DeleteAsync("Purge bot command executed.");

			await context.LogActionAsync();
		}

		[Command("images"), Description("Purge messages with images or attachments on them."), Aliases("i", "imgs", "img"),
	 RequirePermissions(Permissions.ManageMessages), CheckDisable]
		public async Task PurgeImagesAsync(CommandContext context)
		{
			var ms = await context.Channel.GetMessagesBeforeAsync(context.Message.Id, 100);
			Regex ImageRegex = new Regex(@"\.(png|gif|jpg|jpeg|tiff|webp)");
			var deleteThis = ms.Where(m => ImageRegex.IsMatch(m.Content) || m.Attachments.Any()).ToList();
			if (deleteThis.Any())
				await context.Channel.DeleteMessagesAsync(deleteThis, "Purged images");
			var response = await context.SafeRespondUnformattedAsync("✅ Latest messages deleted.");
			await Task.Delay(2000);
			await response.DeleteAsync("Image purge command executed.");
			await context.Message.DeleteAsync("Image purge command executed.");

			await context.LogActionAsync();
		}

		private static List<string> Tokenize(string value, char sep, char block)
		{
			var result = new List<string>();
			var sb = new StringBuilder();
			var insideBlock = false;
			foreach (var c in value)
			{
				if (insideBlock && c == '\\')
				{
					continue;
				}
				if (c == block)
				{
					insideBlock = !insideBlock;
				}
				else if (c == sep && !insideBlock)
				{
					if (sb.IsNullOrWhitespace()) continue;
					result.Add(sb.ToString().Trim());
					sb.Clear();
				}
				else
				{
					sb.Append(c);
				}
			}
			if (sb.ToString().Trim().Length > 0)
			{
				result.Add(sb.ToString().Trim());
			}

			return result;
		}
	}
}
