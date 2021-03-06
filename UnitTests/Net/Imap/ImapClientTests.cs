﻿//
// ImapClientTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

using NUnit.Framework;

using MimeKit;

using MailKit.Net.Imap;
using MailKit.Security;
using MailKit.Search;
using MailKit;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapClientTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		static readonly ImapCapabilities GreetingCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Namespace | ImapCapabilities.Unselect;
		static readonly ImapCapabilities DovecotInitialCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.LiteralPlus | ImapCapabilities.SaslIR | ImapCapabilities.LoginReferrals | ImapCapabilities.Id |
			ImapCapabilities.Enable | ImapCapabilities.Idle;
		static readonly ImapCapabilities DovecotAuthenticatedCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.LiteralPlus | ImapCapabilities.SaslIR | ImapCapabilities.LoginReferrals | ImapCapabilities.Id |
			ImapCapabilities.Enable | ImapCapabilities.Idle | ImapCapabilities.Sort | ImapCapabilities.SortDisplay |
			ImapCapabilities.Thread | ImapCapabilities.MultiAppend | ImapCapabilities.Catenate | ImapCapabilities.Unselect |
			ImapCapabilities.Children | ImapCapabilities.Namespace | ImapCapabilities.UidPlus | ImapCapabilities.ListExtended |
			ImapCapabilities.I18NLevel | ImapCapabilities.CondStore | ImapCapabilities.QuickResync | ImapCapabilities.ESearch |
			ImapCapabilities.ESort | ImapCapabilities.SearchResults | ImapCapabilities.Within | ImapCapabilities.Context |
			ImapCapabilities.ListStatus | ImapCapabilities.Binary | ImapCapabilities.Move | ImapCapabilities.SpecialUse;
		static readonly ImapCapabilities GMailInitialCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Quota | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Id |
			ImapCapabilities.Children | ImapCapabilities.Unselect | ImapCapabilities.SaslIR | ImapCapabilities.XList |
			ImapCapabilities.GMailExt1;
		static readonly ImapCapabilities GMailAuthenticatedCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Quota | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Id |
			ImapCapabilities.Children | ImapCapabilities.Unselect | ImapCapabilities.UidPlus | ImapCapabilities.CondStore |
			ImapCapabilities.ESearch | ImapCapabilities.Compress | ImapCapabilities.Enable | ImapCapabilities.ListExtended |
			ImapCapabilities.ListStatus | ImapCapabilities.Move | ImapCapabilities.UTF8Accept | ImapCapabilities.XList |
			ImapCapabilities.GMailExt1 | ImapCapabilities.LiteralMinus | ImapCapabilities.AppendLimit;
		static readonly ImapCapabilities AclInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities AclAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities MetadataInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Metadata;
		static readonly ImapCapabilities MetadataAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Metadata;

		static FolderAttributes GetSpecialFolderAttribute (SpecialFolder special)
		{
			switch (special) {
			case SpecialFolder.All:     return FolderAttributes.All;
			case SpecialFolder.Archive: return FolderAttributes.Archive;
			case SpecialFolder.Drafts:  return FolderAttributes.Drafts;
			case SpecialFolder.Flagged: return FolderAttributes.Flagged;
			case SpecialFolder.Junk:    return FolderAttributes.Junk;
			case SpecialFolder.Sent:    return FolderAttributes.Sent;
			case SpecialFolder.Trash:   return FolderAttributes.Trash;
			default: throw new ArgumentOutOfRangeException ();
			}
		}

		Stream GetResourceStream (string name)
		{
			return GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
		}

		static string HexEncode (byte[] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\"\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\"\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 143, false));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 143, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 143, false));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 143, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Authenticate
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ("username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ("username", null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, credentials));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, credentials));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, "username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, "username", null));

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var dates = new List<DateTimeOffset> ();
				var messages = new List<MimeMessage> ();
				var flags = new List<MessageFlags> ();
				var now = DateTimeOffset.Now;

				messages.Add (CreateThreadableMessage ("A", "<a@mimekit.net>", null, now.AddMinutes (-7)));
				messages.Add (CreateThreadableMessage ("B", "<b@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-6)));
				messages.Add (CreateThreadableMessage ("C", "<c@mimekit.net>", "<a@mimekit.net> <b@mimekit.net>", now.AddMinutes (-5)));
				messages.Add (CreateThreadableMessage ("D", "<d@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-4)));
				messages.Add (CreateThreadableMessage ("E", "<e@mimekit.net>", "<c@mimekit.net> <x@mimekit.net> <y@mimekit.net> <z@mimekit.net>", now.AddMinutes (-3)));
				messages.Add (CreateThreadableMessage ("F", "<f@mimekit.net>", "<b@mimekit.net>", now.AddMinutes (-2)));
				messages.Add (CreateThreadableMessage ("G", "<g@mimekit.net>", null, now.AddMinutes (-1)));
				messages.Add (CreateThreadableMessage ("H", "<h@mimekit.net>", null, now));

				for (int i = 0; i < messages.Count; i++) {
					dates.Add (DateTimeOffset.Now);
					flags.Add (MessageFlags.Seen);
				}

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				// ImapFolder .ctor
				Assert.Throws<ArgumentNullException> (() => new ImapFolder (null));

				// Open
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Open ((FolderAccess) 500));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Open ((FolderAccess) 500, 0, 0, UniqueIdRange.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.OpenAsync ((FolderAccess) 500));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.OpenAsync ((FolderAccess) 500, 0, 0, UniqueIdRange.All));

				// Create
				Assert.Throws<ArgumentNullException> (() => inbox.Create (null, true));
				Assert.Throws<ArgumentException> (() => inbox.Create (string.Empty, true));
				Assert.Throws<ArgumentException> (() => inbox.Create ("Folder./Name", true));
				Assert.Throws<ArgumentNullException> (() => inbox.Create (null, SpecialFolder.All));
				Assert.Throws<ArgumentException> (() => inbox.Create (string.Empty, SpecialFolder.All));
				Assert.Throws<ArgumentException> (() => inbox.Create ("Folder./Name", SpecialFolder.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Create (null, new SpecialFolder[] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (() => inbox.Create (string.Empty, new SpecialFolder[] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (() => inbox.Create ("Folder./Name", new SpecialFolder[] { SpecialFolder.All }));
				Assert.Throws<ArgumentNullException> (() => inbox.Create ("ValidName", null));
				Assert.Throws<NotSupportedException> (() => inbox.Create ("ValidName", SpecialFolder.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync (null, true));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, true));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync (null, SpecialFolder.All));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, SpecialFolder.All));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", SpecialFolder.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync (null, new SpecialFolder[] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, new SpecialFolder[] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", new SpecialFolder[] { SpecialFolder.All }));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync ("ValidName", null));
				Assert.Throws<NotSupportedException> (async () => await inbox.CreateAsync ("ValidName", SpecialFolder.All));

				// Rename
				Assert.Throws<ArgumentNullException> (() => inbox.Rename (null, "NewName"));
				Assert.Throws<ArgumentNullException> (() => inbox.Rename (personal, null));
				Assert.Throws<ArgumentException> (() => inbox.Rename (personal, string.Empty));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RenameAsync (null, "NewName"));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RenameAsync (personal, null));
				Assert.Throws<ArgumentException> (async () => await inbox.RenameAsync (personal, string.Empty));

				// GetSubfolder
				Assert.Throws<ArgumentNullException> (() => inbox.GetSubfolder (null));
				Assert.Throws<ArgumentException> (() => inbox.GetSubfolder (string.Empty));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetSubfolderAsync (null));
				Assert.Throws<ArgumentException> (async () => await inbox.GetSubfolderAsync (string.Empty));

				// GetMetadata
				Assert.Throws<ArgumentNullException> (() => client.GetMetadata (null, new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (() => client.GetMetadata (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (async () => await client.GetMetadataAsync (null, new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (async () => await client.GetMetadataAsync (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (() => inbox.GetMetadata (null, new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (() => inbox.GetMetadata (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetMetadataAsync (null, new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetMetadataAsync (new MetadataOptions (), null));

				// SetMetadata
				Assert.Throws<ArgumentNullException> (() => client.SetMetadata (null));
				Assert.Throws<ArgumentNullException> (async () => await client.SetMetadataAsync (null));
				Assert.Throws<ArgumentNullException> (() => inbox.SetMetadata (null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetMetadataAsync (null));

				// Expunge
				Assert.Throws<ArgumentNullException> (() => inbox.Expunge (null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ExpungeAsync (null));

				// Append
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages[0]));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages[0]));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, MessageFlags.None, DateTimeOffset.Now));

				// MultiAppend
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, flags));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (messages, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages, flags));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, flags));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, flags, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, null, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (messages, null, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, flags, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (messages, flags, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages, flags, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, flags, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, null, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, null, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, flags, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, flags, null));

				// CopyTo
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo (new int[] { 0 }, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync (new int[] { 0 }, null));

				// MoveTo
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo (new int[] { 0 }, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync (new int[] { 0 }, null));

				// Fetch
				var headers = new HashSet<HeaderId> (new HeaderId[] { HeaderId.Subject });
				var fields = new HashSet<string> (new string[] { "SUBJECT" });
				var uids = new UniqueId[] { UniqueId.MinValue };
				var emptyHeaders = new HashSet<HeaderId> ();
				var emptyFields = new HashSet<string> ();
				var indexes = new int[] { 0 };

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, emptyFields));

				// Fetch + modseq
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, emptyFields));

				// GetMessage
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetMessage (-1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetMessageAsync (-1));
				Assert.Throws<ArgumentException> (() => inbox.GetMessage (UniqueId.Invalid));
				Assert.Throws<ArgumentException> (async () => await inbox.GetMessageAsync (UniqueId.Invalid));

				// GetBodyPart
				var bodyPart = new BodyPartText ();

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, bodyPart));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (BodyPart) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (BodyPart) null));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, "1.2"));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (string) null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, "1.2"));
				//Assert.Throws<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (string) null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (string) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, bodyPart, true));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, bodyPart, true));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (BodyPart) null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (BodyPart) null, true));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, bodyPart, true));
				Assert.Throws<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, bodyPart, true));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (BodyPart) null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (BodyPart) null, true));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, "1.2", true));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, "1.2", true));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (string) null, true));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (string) null, true));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, "1.2", true));
				//Assert.Throws<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, "1.2", true));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (string) null, true));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (string) null, true));

				// GetStream
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2"));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string) null));

				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart));
				//Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart) null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart) null));

				//Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart));
				//Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart));
				//Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart) null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, 0, 1024));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, 0, -1));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2", 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2", 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string) null, 0, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string) null, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, "1.2", 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, "1.2", 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2", 0, 1024));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2", 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string) null, 0, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string) null, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, "1.2", 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, "1.2", 0, -1));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart, 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart) null, -1, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart) null, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, bodyPart, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, bodyPart, 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart, 0, 1024));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart, 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart) null, -1, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart) null, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, bodyPart, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, bodyPart, 0, -1));

				// AddFlags
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (0, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (0, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueId.MinValue, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (UniqueId.MinValue, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (new int[] { 0 }, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (new int[] { 0 }, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueIdRange.All, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (UniqueIdRange.All, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (new int[] { 0 }, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (new int[] { 0 }, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueIdRange.All, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddFlagsAsync (UniqueIdRange.All, 1, MessageFlags.None, true));

				// RemoveFlags
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (0, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (0, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueId.MinValue, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (UniqueId.MinValue, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (new int[] { 0 }, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (new int[] { 0 }, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueIdRange.All, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (UniqueIdRange.All, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (new int[] { 0 }, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (new int[] { 0 }, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueIdRange.All, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveFlagsAsync (UniqueIdRange.All, 1, MessageFlags.None, true));

				// SetFlags
				Assert.Throws<ArgumentException> (() => inbox.SetFlags (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (async () => await inbox.SetFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));

				var labels = new string[] { "Label1", "Label2" };
				var emptyLabels = new string[0];

				// AddLabels
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (-1, labels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (0, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (new int[] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync (new int[] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (new int[] { 0 }, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddLabelsAsync (new int[] { 0 }, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (UniqueIdRange.All, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, emptyLabels, true));

				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (new int[] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync (new int[] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (new int[] { 0 }, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddLabelsAsync (new int[] { 0 }, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (UniqueIdRange.All, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, 1, emptyLabels, true));

				// RemoveLabels
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (-1, labels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (0, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (new int[] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (new int[] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (new int[] { 0 }, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveLabelsAsync (new int[] { 0 }, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (UniqueIdRange.All, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, emptyLabels, true));

				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (new int[] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (new int[] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (new int[] { 0 }, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveLabelsAsync (new int[] { 0 }, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (UniqueIdRange.All, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, 1, emptyLabels, true));

				// SetLabels
				Assert.Throws<ArgumentException> (() => inbox.SetLabels (-1, labels, true));
				Assert.Throws<ArgumentException> (async () => await inbox.SetLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (0, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (new int[] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync (new int[] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync (UniqueIdRange.All, null, true));

				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (new int[] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync (new int[] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetLabelsAsync (UniqueIdRange.All, 1, null, true));

				// Search
				var searchOptions = SearchOptions.All | SearchOptions.Min | SearchOptions.Max | SearchOptions.Count;
				var orderBy = new OrderBy[] { OrderBy.Arrival };
				var emptyOrderBy = new OrderBy[0];

				Assert.Throws<ArgumentNullException> (() => inbox.Search ((SearchQuery) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Search (SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SearchAsync (SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search ((IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Search (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SearchAsync (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Search (searchOptions, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SearchAsync (searchOptions, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, (IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Search (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SearchAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Search ((string) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((string) null));

				// Sort
				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (searchOptions, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (searchOptions, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((string) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync ((string) null));

				// Thread
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Thread ((ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.ThreadAsync ((ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread (ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ThreadAsync (ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread ((IList<UniqueId>) null, ThreadingAlgorithm.References, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ThreadAsync ((IList<UniqueId>) null, ThreadingAlgorithm.References, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Thread (UniqueIdRange.All, (ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.ThreadAsync (UniqueIdRange.All, (ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread (UniqueIdRange.All, ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.References, null));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestImapClientGreetingCapabilities ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GreetingCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
			}
		}

		[Test]
		public async void TestImapClientFeatures ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\")\r\n", "common.id.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 GETQUOTAROOT INBOX\r\n", "common.getquota.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 SETQUOTA \"\" (MESSAGE 1000000 STORAGE 5242880)\r\n", "common.setquota.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var implementation = new ImapImplementation {
					Name = "MailKit", Version = "1.0", Vendor = "Xamarin Inc."
				};

				implementation = await client.IdentifyAsync (implementation);
				Assert.IsNotNull (implementation, "Expected a non-null ID response.");
				Assert.AreEqual ("GImap", implementation.Name);
				Assert.AreEqual ("Google, Inc.", implementation.Vendor);
				Assert.AreEqual ("http://support.google.com/mail", implementation.SupportUrl);
				Assert.AreEqual ("gmail_imap_150623.03_p1", implementation.Version);
				Assert.AreEqual ("127.0.0.1", implementation.Properties["remote-host"]);

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = await inbox.GetQuotaAsync ();
				Assert.IsNotNull (quota, "Expected a non-null GETQUOTAROOT response.");
				Assert.AreEqual (personal.FullName, quota.QuotaRoot.FullName);
				Assert.AreEqual (personal, quota.QuotaRoot);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (15728640, quota.StorageLimit.Value);
				Assert.IsFalse (quota.CurrentMessageCount.HasValue);
				Assert.IsFalse (quota.MessageLimit.HasValue);

				quota = await personal.SetQuotaAsync (1000000, 5242880);
				Assert.IsNotNull (quota, "Expected non-null SETQUOTA response.");
				Assert.AreEqual (1107, quota.CurrentMessageCount.Value);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (1000000, quota.MessageLimit.Value);
				Assert.AreEqual (5242880, quota.StorageLimit.Value);

				await client.DisconnectAsync (false);
			}
		}

		static void AssertFolder (IMailFolder folder, string fullName, FolderAttributes attributes, bool subscribed, ulong highestmodseq, int count, int recent, uint uidnext, uint validity, int unread)
		{
			if (subscribed)
				attributes |= FolderAttributes.Subscribed;

			Assert.AreEqual (fullName, folder.FullName, "FullName");
			Assert.AreEqual (attributes, folder.Attributes, "Attributes");
			Assert.AreEqual (subscribed, folder.IsSubscribed, "IsSubscribed");
			Assert.AreEqual (highestmodseq, folder.HighestModSeq, "HighestModSeq");
			Assert.AreEqual (count, folder.Count, "Count");
			Assert.AreEqual (recent, folder.Recent, "Recent");
			Assert.AreEqual (unread, folder.Unread, "Unread");
			Assert.AreEqual (uidnext, folder.UidNext.HasValue ? folder.UidNext.Value.Id : (uint) 0, "UidNext");
			Assert.AreEqual (validity, folder.UidValidity, "UidValidity");
		}

		[Test]
		public async void TestImapClientGetFolders ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST (SUBSCRIBED) \"\" \"*\" RETURN (CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ))\r\n", "gmail.list-all.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;
				var folders = (await client.GetFoldersAsync (client.PersonalNamespaces[0], all, true)).ToList ();
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0);
				AssertFolder (folders[1], "[Gmail]", FolderAttributes.HasChildren | FolderAttributes.NonExistent | FolderAttributes.NoSelect, true, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (folders[3], "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (folders[4], "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (folders[6], "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (folders[7], "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (folders[8], "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				AssertFolder (client.Inbox, "INBOX", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				//AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				client.Disconnect (false);
			}
		}

		static MimeMessage CreateThreadableMessage (string subject, string msgid, string references, DateTimeOffset date)
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.To.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.MessageId = msgid;
			message.Subject = subject;
			message.Date = date;

			if (references != null) {
				foreach (var reference in references.Split (' '))
					message.References.Add (reference);
			}

			message.Body = new TextPart ("plain") { Text = "This is the message body.\r\n" };

			return message;
		}

		[Test]
		public async void TestImapClientDovecot ()
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\"\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\"\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 ENABLE QRESYNC CONDSTORE\r\n", "dovecot.enable-qresync.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\" RETURN (SUBSCRIBED CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ))\r\n", "dovecot.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 CREATE UnitTests.\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "dovecot.list-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 CREATE UnitTests.Messages\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000009 LIST \"\" UnitTests.Messages\r\n", "dovecot.list-unittests-messages.txt"));

			var command = new StringBuilder ("A00000010 APPEND UnitTests.Messages");
			var internalDates = new List<DateTimeOffset> ();
			var messages = new List<MimeMessage> ();
			var flags = new List<MessageFlags> ();
			var now = DateTimeOffset.Now;

			messages.Add (CreateThreadableMessage ("A", "<a@mimekit.net>", null, now.AddMinutes (-7)));
			messages.Add (CreateThreadableMessage ("B", "<b@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-6)));
			messages.Add (CreateThreadableMessage ("C", "<c@mimekit.net>", "<a@mimekit.net> <b@mimekit.net>", now.AddMinutes (-5)));
			messages.Add (CreateThreadableMessage ("D", "<d@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-4)));
			messages.Add (CreateThreadableMessage ("E", "<e@mimekit.net>", "<c@mimekit.net> <x@mimekit.net> <y@mimekit.net> <z@mimekit.net>", now.AddMinutes (-3)));
			messages.Add (CreateThreadableMessage ("F", "<f@mimekit.net>", "<b@mimekit.net>", now.AddMinutes (-2)));
			messages.Add (CreateThreadableMessage ("G", "<g@mimekit.net>", null, now.AddMinutes (-1)));
			messages.Add (CreateThreadableMessage ("H", "<h@mimekit.net>", null, now));

			for (int i = 0; i < messages.Count; i++) {
				var message = messages[i];
				string latin1;
				long length;

				internalDates.Add (messages[i].Date);
				flags.Add (MessageFlags.Draft);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				command.AppendFormat (" (\\Draft) \"{0}\" ", ImapUtils.FormatInternalDate (message.Date));
				command.Append ('{');
				command.AppendFormat ("{0}+", length);
				command.Append ("}\r\n");
				command.Append (latin1);
			}
			command.Append ("\r\n");
			commands.Add (new ImapReplayCommand (command.ToString (), "dovecot.multiappend.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 SELECT UnitTests.Messages (CONDSTORE)\r\n", "dovecot.select-unittests-messages.txt"));
			commands.Add (new ImapReplayCommand ("A00000012 UID STORE 1:8 +FLAGS.SILENT (\\Seen)\r\n", "dovecot.store-seen.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 UID STORE 1:3 +FLAGS.SILENT (\\Answered)\r\n", "dovecot.store-answered.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 UID STORE 8 +FLAGS.SILENT (\\Deleted)\r\n", "dovecot.store-deleted.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 UID EXPUNGE 8\r\n", "dovecot.uid-expunge.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 UID THREAD REFERENCES US-ASCII ALL\r\n", "dovecot.thread-references.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 UID THREAD ORDEREDSUBJECT US-ASCII UID 1:* ALL\r\n", "dovecot.thread-orderedsubject.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 UNSELECT\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000019 SELECT UnitTests.Messages (QRESYNC (1436832084 2 1:8))\r\n", "dovecot.select-unittests-messages-qresync.txt"));
			commands.Add (new ImapReplayCommand ("A00000020 UID SEARCH RETURN (ALL COUNT MIN MAX) MODSEQ 2\r\n", "dovecot.search-changed-since.txt"));
			commands.Add (new ImapReplayCommand ("A00000021 UID FETCH 1:7 (UID FLAGS MODSEQ)\r\n", "dovecot.fetch1.txt"));
			commands.Add (new ImapReplayCommand ("A00000022 UID FETCH 1:* (UID FLAGS MODSEQ) (CHANGEDSINCE 2 VANISHED)\r\n", "dovecot.fetch2.txt"));
			commands.Add (new ImapReplayCommand ("A00000023 UID SORT RETURN (ALL COUNT MIN MAX) (REVERSE ARRIVAL) US-ASCII ALL\r\n", "dovecot.sort-reverse-arrival.txt"));
			commands.Add (new ImapReplayCommand ("A00000024 UID SEARCH RETURN () UNDELETED SEEN\r\n", "dovecot.optimized-search.txt"));
			commands.Add (new ImapReplayCommand ("A00000025 CREATE UnitTests.Destination\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000026 LIST \"\" UnitTests.Destination\r\n", "dovecot.list-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000027 UID COPY 1:7 UnitTests.Destination\r\n", "dovecot.copy.txt"));
			commands.Add (new ImapReplayCommand ("A00000028 UID MOVE 1:7 UnitTests.Destination\r\n", "dovecot.move.txt"));
			commands.Add (new ImapReplayCommand ("A00000029 STATUS UnitTests.Destination (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "dovecot.status-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000030 SELECT UnitTests.Destination (CONDSTORE)\r\n", "dovecot.select-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000031 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1 VANISHED)\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000032 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch4.txt"));
			commands.Add (new ImapReplayCommand ("A00000033 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch5.txt"));
			commands.Add (new ImapReplayCommand ("A00000034 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch6.txt"));
			commands.Add (new ImapReplayCommand ("A00000035 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch7.txt"));
			commands.Add (new ImapReplayCommand ("A00000036 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch8.txt"));
			commands.Add (new ImapReplayCommand ("A00000037 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch9.txt"));
			commands.Add (new ImapReplayCommand ("A00000038 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch10.txt"));
			commands.Add (new ImapReplayCommand ("A00000039 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)])\r\n", "dovecot.fetch11.txt"));
			commands.Add (new ImapReplayCommand ("A00000040 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)])\r\n", "dovecot.fetch12.txt"));
			commands.Add (new ImapReplayCommand ("A00000041 UID FETCH 1 (BODY.PEEK[HEADER] BODY.PEEK[TEXT])\r\n", "dovecot.getbodypart.txt"));
			commands.Add (new ImapReplayCommand ("A00000042 FETCH 1 (BODY.PEEK[HEADER] BODY.PEEK[TEXT])\r\n", "dovecot.getbodypart2.txt"));
			commands.Add (new ImapReplayCommand ("A00000043 UID FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream.txt"));
			commands.Add (new ImapReplayCommand ("A00000044 UID FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream2.txt"));
			commands.Add (new ImapReplayCommand ("A00000045 FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream3.txt"));
			commands.Add (new ImapReplayCommand ("A00000046 FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream4.txt"));
			commands.Add (new ImapReplayCommand ("A00000047 UID FETCH 1 (BODY.PEEK[HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)])\r\n", "dovecot.getstream-section.txt"));
			commands.Add (new ImapReplayCommand ("A00000048 FETCH 1 (BODY.PEEK[HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)])\r\n", "dovecot.getstream-section2.txt"));
			commands.Add (new ImapReplayCommand ("A00000049 UID STORE 1:14 (UNCHANGEDSINCE 3) +FLAGS.SILENT (\\Deleted $MailKit)\r\n", "dovecot.store-deleted-custom.txt"));
			commands.Add (new ImapReplayCommand ("A00000050 STORE 1:7 (UNCHANGEDSINCE 5) FLAGS.SILENT (\\Deleted \\Seen $MailKit)\r\n", "dovecot.setflags-unchangedsince.txt"));
			commands.Add (new ImapReplayCommand ("A00000051 UID SEARCH RETURN () UID 1:14 OR ANSWERED OR DELETED OR DRAFT OR FLAGGED OR RECENT OR UNANSWERED OR UNDELETED OR UNDRAFT OR UNFLAGGED OR UNSEEN OR KEYWORD $MailKit UNKEYWORD $MailKit\r\n", "dovecot.search-uids.txt"));
			commands.Add (new ImapReplayCommand ("A00000052 UID SEARCH RETURN (ALL COUNT MIN MAX) UID 1:14 LARGER 256 SMALLER 512\r\n", "dovecot.search-uids-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000053 UID SORT RETURN () (REVERSE DATE SUBJECT DISPLAYFROM SIZE) US-ASCII OR OR (SENTBEFORE 12-Oct-2016 SENTSINCE 10-Oct-2016) NOT SENTON 11-Oct-2016 OR (BEFORE 12-Oct-2016 SINCE 10-Oct-2016) NOT ON 11-Oct-2016\r\n", "dovecot.sort-by-date.txt"));
			commands.Add (new ImapReplayCommand ("A00000054 UID SORT RETURN () (FROM TO CC) US-ASCII UID 1:14 OR BCC xyz OR CC xyz OR FROM xyz OR TO xyz OR SUBJECT xyz OR HEADER Message-Id mimekit.net OR BODY \"This is the message body.\" TEXT message\r\n", "dovecot.sort-by-strings.txt"));
			commands.Add (new ImapReplayCommand ("A00000055 UID SORT RETURN (ALL COUNT MIN MAX) (DISPLAYTO) US-ASCII UID 1:14 OLDER 1 YOUNGER 3600\r\n", "dovecot.sort-uids-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000056 UID SEARCH ALL\r\n", "dovecot.search-raw.txt"));
			commands.Add (new ImapReplayCommand ("A00000057 UID SORT (REVERSE ARRIVAL) US-ASCII ALL\r\n", "dovecot.sort-raw.txt"));
			commands.Add (new ImapReplayCommand ("A00000058 EXPUNGE\r\n", "dovecot.expunge.txt"));
			commands.Add (new ImapReplayCommand ("A00000059 CLOSE\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (DovecotInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (DovecotAuthenticatedCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.InternationalizationLevel, "Expected I18NLEVEL=1");
				Assert.IsTrue (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.IsTrue (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");
				// TODO: verify CONTEXT=SEARCH

				var personal = client.GetFolder (client.PersonalNamespaces[0]);

				// Make sure these all throw NotSupportedException
				Assert.Throws<NotSupportedException> (() => client.EnableUTF8 ());
				Assert.Throws<NotSupportedException> (() => client.Inbox.GetAccessRights ("smith"));
				Assert.Throws<NotSupportedException> (() => client.Inbox.GetMyAccessRights ());
				var rights = new AccessRights ("lrswida");
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddAccessRights ("smith", rights));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveAccessRights ("smith", rights));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetAccessRights ("smith", rights));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveAccess ("smith"));
				Assert.Throws<NotSupportedException> (() => client.Inbox.GetQuota ());
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetQuota (null, null));
				Assert.Throws<NotSupportedException> (() => client.GetMetadata (MetadataTag.PrivateComment));
				Assert.Throws<NotSupportedException> (() => client.GetMetadata (new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<NotSupportedException> (() => client.SetMetadata (new MetadataCollection ()));
				var labels = new string[] { "Label1", "Label2" };
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (UniqueId.MinValue, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (UniqueIdRange.All, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (UniqueIdRange.All, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (0, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (new int[] { 0 }, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (new int[] { 0 }, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (UniqueId.MinValue, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (UniqueIdRange.All, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (UniqueIdRange.All, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (0, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (new int[] { 0 }, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (new int[] { 0 }, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (UniqueId.MinValue, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (UniqueIdRange.All, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (UniqueIdRange.All, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (0, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (new int[] { 0 }, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (new int[] { 0 }, 1, labels, true));

				try {
					await client.EnableQuickResyncAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception when enabling QRESYNC: {0}", ex);
				}

				// take advantage of LIST-STATUS to get top-level personal folders...
				var statusItems = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;

				var folders = (await personal.GetSubfoldersAsync (statusItems, false)).ToArray ();
				Assert.AreEqual (6, folders.Length, "Expected 6 folders");

				var expectedFolderNames = new [] { "Archives", "Drafts", "Junk", "Sent Messages", "Trash", "INBOX" };
				var expectedUidValidities = new [] { 1436832059, 1436832060, 1436832061, 1436832062, 1436832063, 1436832057 };
				var expectedHighestModSeq = new [] { 1, 1, 1, 1, 1, 15 };
				var expectedMessages = new [] { 0, 0, 0, 0, 0, 4 };
				var expectedUidNext = new [] { 1, 1, 1, 1, 1, 5 };
				var expectedRecent = new [] { 0, 0, 0, 0, 0, 0 };
				var expectedUnseen = new [] { 0, 0, 0, 0, 0, 0 };

				for (int i = 0; i < folders.Length; i++) {
					Assert.AreEqual (expectedFolderNames[i], folders[i].FullName, "FullName did not match");
					Assert.AreEqual (expectedFolderNames[i], folders[i].Name, "Name did not match");
					Assert.AreEqual (expectedUidValidities[i], folders[i].UidValidity, "UidValidity did not match");
					Assert.AreEqual (expectedHighestModSeq[i], folders[i].HighestModSeq, "HighestModSeq did not match");
					Assert.AreEqual (expectedMessages[i], folders[i].Count, "Count did not match");
					Assert.AreEqual (expectedRecent[i], folders[i].Recent, "Recent did not match");
					Assert.AreEqual (expectedUnseen[i], folders[i].Unread, "Unread did not match");
				}

				var unitTests = await personal.CreateAsync ("UnitTests", false);
				Assert.AreEqual (FolderAttributes.HasNoChildren, unitTests.Attributes, "Unexpected UnitTests folder attributes");

				var folder = await unitTests.CreateAsync ("Messages", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, folder.Attributes, "Unexpected UnitTests.Messages folder attributes");
				//Assert.AreEqual (FolderAttributes.HasChildren, unitTests.Attributes, "Expected UnitTests Attributes to be updated");

				// Use MULTIAPPEND to append some test messages
				var appended = await folder.AppendAsync (messages, flags, internalDates);
				Assert.AreEqual (8, appended.Count, "Unexpected number of messages appended");

				// SELECT the folder so that we can test some stuff
				var access = await folder.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (expectedPermanentFlags, folder.PermanentFlags, "UnitTests.Messages PERMANENTFLAGS");
				Assert.AreEqual (expectedFlags, folder.AcceptedFlags, "UnitTests.Messages FLAGS");
				Assert.AreEqual (8, folder.Count, "UnitTests.Messages EXISTS");
				Assert.AreEqual (8, folder.Recent, "UnitTests.Messages RECENT");
				Assert.AreEqual (0, folder.FirstUnread, "UnitTests.Messages UNSEEN");
				Assert.AreEqual (1436832084U, folder.UidValidity, "UnitTests.Messages UIDVALIDITY");
				Assert.AreEqual (9, folder.UidNext.Value.Id, "UnitTests.Messages UIDNEXT");
				Assert.AreEqual (2UL, folder.HighestModSeq, "UnitTests.Messages HIGHESTMODSEQ");
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Messages to be opened in READ-WRITE mode");

				// Keep track of various folder events
				var flagsChanged = new List<MessageFlagsChangedEventArgs> ();
				var modSeqChanged = new List<ModSeqChangedEventArgs> ();
				var vanished = new List<MessagesVanishedEventArgs> ();
				bool recentChanged = false;

				folder.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				folder.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				folder.MessagesVanished += (sender, e) => {
					vanished.Add (e);
				};

				folder.RecentChanged += (sender, e) => {
					recentChanged = true;
				};

				// Keep track of UIDVALIDITY and HIGHESTMODSEQ values for our QRESYNC test later
				var highestModSeq = folder.HighestModSeq;
				var uidValidity = folder.UidValidity;

				// Make some FLAGS changes to our messages so we can test QRESYNC
				await folder.AddFlagsAsync (appended, MessageFlags.Seen, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (8, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected modSeqChanged[{0}].Index", i);
					Assert.AreEqual (i + 1, modSeqChanged[i].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", i);
					Assert.AreEqual (3, modSeqChanged[i].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", i);
				}
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				var answered = new UniqueIdSet (SortOrder.Ascending);
				answered.Add (appended[0]); // A
				answered.Add (appended[1]); // B
				answered.Add (appended[2]); // C
				await folder.AddFlagsAsync (answered, MessageFlags.Answered, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (3, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected modSeqChanged[{0}].Index", i);
					Assert.AreEqual (i + 1, modSeqChanged[i].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", i);
					Assert.AreEqual (4, modSeqChanged[i].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", i);
				}
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				// Delete some messages so we can test that QRESYNC emits some MessageVanished events
				// both now *and* when we use QRESYNC to re-open the folder
				var deleted = new UniqueIdSet (SortOrder.Ascending);
				deleted.Add (appended[7]); // H
				await folder.AddFlagsAsync (deleted, MessageFlags.Deleted, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (1, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (7, modSeqChanged[0].Index, "Unexpected modSeqChanged[{0}].Index", 0);
				Assert.AreEqual (8, modSeqChanged[0].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", 0);
				Assert.AreEqual (5, modSeqChanged[0].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", 0);
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				await folder.ExpungeAsync (deleted);
				Assert.AreEqual (1, vanished.Count, "Expected MessagesVanished event");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				Assert.IsFalse (vanished[0].Earlier, "Expected EARLIER to be false");
				Assert.IsTrue (recentChanged, "Expected RecentChanged event");
				recentChanged = false;
				vanished.Clear ();

				// Verify that THREAD works correctly
				var threaded = await folder.ThreadAsync (ThreadingAlgorithm.References, SearchQuery.All);
				Assert.AreEqual (2, threaded.Count, "Unexpected number of root nodes in threaded results");

				threaded = await folder.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.OrderedSubject, SearchQuery.All);
				Assert.AreEqual (7, threaded.Count, "Unexpected number of root nodes in threaded results");

				// UNSELECT the folder so we can re-open it using QRESYNC
				await folder.CloseAsync ();

				// Use QRESYNC to get the changes since last time we opened the folder
				access = await folder.OpenAsync (FolderAccess.ReadWrite, uidValidity, highestModSeq, appended);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Messages to be opened in READ-WRITE mode");
				Assert.AreEqual (7, flagsChanged.Count, "Unexpected number of MessageFlagsChanged events");
				Assert.AreEqual (7, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < flagsChanged.Count; i++) {
					var messageFlags = MessageFlags.Seen | MessageFlags.Draft;

					if (i < 3)
						messageFlags |= MessageFlags.Answered;

					Assert.AreEqual (i, flagsChanged[i].Index, "Unexpected value for flagsChanged[{0}].Index", i);
					Assert.AreEqual ((uint) (i + 1), flagsChanged[i].UniqueId.Value.Id, "Unexpected value for flagsChanged[{0}].UniqueId", i);
					Assert.AreEqual (messageFlags, flagsChanged[i].Flags, "Unexpected value for flagsChanged[{0}].Flags", i);

					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					if (i < 3)
						Assert.AreEqual (4, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
					else
						Assert.AreEqual (3, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				Assert.AreEqual (1, vanished.Count, "Unexpected number of MessagesVanished events");
				Assert.IsTrue (vanished[0].Earlier, "Expected VANISHED EARLIER");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				vanished.Clear ();

				// Use SEARCH and FETCH to get the same info
				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max;
				var changed = await folder.SearchAsync (searchOptions, SearchQuery.ChangedSince (highestModSeq));
				Assert.AreEqual (7, changed.UniqueIds.Count, "Unexpected number of UIDs");
				Assert.IsTrue (changed.ModSeq.HasValue, "Expected the ModSeq property to be set");
				Assert.AreEqual (4, changed.ModSeq.Value, "Unexpected ModSeq value");
				Assert.AreEqual (1, changed.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (7, changed.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (7, changed.Count, "Unexpected Count");

				var fetched = await folder.FetchAsync (changed.UniqueIds, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.AreEqual (7, fetched.Count, "Unexpected number of messages fetched");
				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");
				}

				// or... we could just use a single UID FETCH command like so:
				fetched = await folder.FetchAsync (UniqueIdRange.All, highestModSeq, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");
				}
				Assert.AreEqual (7, fetched.Count, "Unexpected number of messages fetched");
				Assert.AreEqual (1, vanished.Count, "Unexpected number of MessagesVanished events");
				Assert.IsTrue (vanished[0].Earlier, "Expected VANISHED EARLIER");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				vanished.Clear ();

				// Use SORT to order by reverse arrival order
				var orderBy = new OrderBy[] { new OrderBy (OrderByType.Arrival, SortOrder.Descending) };
				var sorted = await folder.SearchAsync (searchOptions, SearchQuery.All, orderBy);
				Assert.AreEqual (7, sorted.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.AreEqual (7 - i, sorted.UniqueIds[i].Id, "Unexpected value for UniqueId[{0}]", i);
				Assert.IsFalse (sorted.ModSeq.HasValue, "Expected the ModSeq property to be null");
				Assert.AreEqual (7, sorted.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (1, sorted.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (7, sorted.Count, "Unexpected Count");

				// Verify that optimizing NOT queries works correctly
				var uids = await folder.SearchAsync (SearchQuery.Not (SearchQuery.Deleted).And (SearchQuery.Not (SearchQuery.NotSeen)));
				Assert.AreEqual (7, uids.Count, "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected value for uids[{0}]", i);

				// Create a Destination folder to use for copying/moving messages to
				var destination = await unitTests.CreateAsync ("Destination", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, destination.Attributes, "Unexpected UnitTests.Destination folder attributes");

				// COPY messages to the Destination folder
				var copied = await folder.CopyToAsync (uids, destination);
				Assert.AreEqual (uids.Count, copied.Source.Count, "Unexpetced Source.Count");
				Assert.AreEqual (uids.Count, copied.Destination.Count, "Unexpetced Destination.Count");

				// MOVE messages to the Destination folder
				var moved = await folder.MoveToAsync (uids, destination);
				Assert.AreEqual (uids.Count, copied.Source.Count, "Unexpetced Source.Count");
				Assert.AreEqual (uids.Count, copied.Destination.Count, "Unexpetced Destination.Count");
				Assert.AreEqual (1, vanished.Count, "Expected VANISHED event");
				vanished.Clear ();

				await destination.StatusAsync (statusItems);
				Assert.AreEqual (moved.Destination[0].Validity, destination.UidValidity, "Unexpected UIDVALIDITY");

				destination.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				destination.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				destination.MessagesVanished += (sender, e) => {
					vanished.Add (e);
				};

				destination.RecentChanged += (sender, e) => {
					recentChanged = true;
				};

				await destination.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Destination to be opened in READ-WRITE mode");

				var fetchHeaders = new HashSet<HeaderId> ();
				fetchHeaders.Add (HeaderId.References);
				fetchHeaders.Add (HeaderId.XMailer);

				var indexes = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

				// Fetch + modseq
				fetched = await destination.FetchAsync (UniqueIdRange.All, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				// Fetch
				fetched = await destination.FetchAsync (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				uids = new UniqueIdSet (SortOrder.Ascending);

				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");

					uids.Add (fetched[i].UniqueId);
				}

				var entity = await destination.GetBodyPartAsync (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.IsInstanceOf<TextPart> (entity);

				entity = await destination.GetBodyPartAsync (fetched[0].Index, fetched[0].TextBody);
				Assert.IsInstanceOf<TextPart> (entity);

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, "", 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, "", 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.AreEqual (62, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.AreEqual (62, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n", text);
				}

				var custom = new HashSet<string> ();
				custom.Add ("$MailKit");

				await destination.AddFlagsAsync (uids, destination.HighestModSeq, MessageFlags.Deleted, custom, true);
				Assert.AreEqual (14, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (5, destination.HighestModSeq);
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					Assert.AreEqual (5, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				modSeqChanged.Clear ();

				await destination.SetFlagsAsync (new int[] { 0, 1, 2, 3, 4, 5, 6 }, destination.HighestModSeq, MessageFlags.Seen | MessageFlags.Deleted, custom, true);
				Assert.AreEqual (7, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (6, destination.HighestModSeq);
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					Assert.AreEqual (6, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				modSeqChanged.Clear ();

				var results = await destination.SearchAsync (uids, SearchQuery.Answered.Or (SearchQuery.Deleted.Or (SearchQuery.Draft.Or (SearchQuery.Flagged.Or (SearchQuery.Recent.Or (SearchQuery.NotAnswered.Or (SearchQuery.NotDeleted.Or (SearchQuery.NotDraft.Or (SearchQuery.NotFlagged.Or (SearchQuery.NotSeen.Or (SearchQuery.HasCustomFlag ("$MailKit").Or (SearchQuery.DoesNotHaveCustomFlag ("$MailKit")))))))))))));
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");

				var matches = await destination.SearchAsync (searchOptions, uids, SearchQuery.LargerThan (256).And (SearchQuery.SmallerThan (512)));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.AreEqual (10, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (13, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (2, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (10, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (expectedMatchedUids[i], matches.UniqueIds[i].Id);

				orderBy = new OrderBy[] { OrderBy.ReverseDate, OrderBy.Subject, OrderBy.DisplayFrom, OrderBy.Size };
				var sentDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.SentBefore (new DateTime (2016, 10, 12)), SearchQuery.SentAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.SentOn (new DateTime (2016, 10, 11))));
				var deliveredDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.DeliveredBefore (new DateTime (2016, 10, 12)), SearchQuery.DeliveredAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.DeliveredOn (new DateTime (2016, 10, 11))));
				results = await destination.SearchAsync (sentDateQuery.Or (deliveredDateQuery), orderBy);
				var expectedSortByDateResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.AreEqual (expectedSortByDateResults[i], results[i].Id);

				var stringQuery = SearchQuery.BccContains ("xyz").Or (SearchQuery.CcContains ("xyz").Or (SearchQuery.FromContains ("xyz").Or (SearchQuery.ToContains ("xyz").Or (SearchQuery.SubjectContains ("xyz").Or (SearchQuery.HeaderContains ("Message-Id", "mimekit.net").Or (SearchQuery.BodyContains ("This is the message body.").Or (SearchQuery.MessageContains ("message"))))))));
				orderBy = new OrderBy[] { OrderBy.From, OrderBy.To, OrderBy.Cc };
				results = await destination.SearchAsync (uids, stringQuery, orderBy);
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.AreEqual (i + 1, results[i].Id);

				orderBy = new OrderBy[] { OrderBy.DisplayTo };
				matches = await destination.SearchAsync (searchOptions, uids, SearchQuery.OlderThan (1).And (SearchQuery.YoungerThan (3600)), orderBy);
				Assert.AreEqual (14, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (14, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (1, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (14, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);

				client.Capabilities &= ~ImapCapabilities.ESearch;
				matches = await ((ImapFolder) destination).SearchAsync ("ALL");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);

				client.Capabilities &= ~ImapCapabilities.ESort;
				matches = await ((ImapFolder) destination).SortAsync ("(REVERSE ARRIVAL) US-ASCII ALL");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);

				await destination.ExpungeAsync ();
				Assert.AreEqual (7, destination.HighestModSeq);
				Assert.AreEqual (1, vanished.Count, "Unexpected number of Vanished events");
				Assert.AreEqual (14, vanished[0].UniqueIds.Count, "Unexpected number of UIDs in Vanished event");
				for (int i = 0; i < vanished[0].UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, vanished[0].UniqueIds[i].Id);
				Assert.IsFalse (vanished[0].Earlier, "Unexpected value for Earlier");
				vanished.Clear ();

				await destination.CloseAsync (true);

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public async void TestImapClientGMail ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 CREATE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "gmail.list-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SELECT UnitTests (CONDSTORE)\r\n", "gmail.select-unittests.txt"));

			for (int i = 0; i < 50; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				var command = string.Format ("A{0:D8} APPEND UnitTests (\\Seen) ", i + 9);

				if (length > 4096) {
					command += "{" + length + "}\r\n";
					commands.Add (new ImapReplayCommand (command, "gmail.go-ahead.txt"));
					commands.Add (new ImapReplayCommand (latin1 + "\r\n", string.Format ("gmail.append.{0}.txt", i + 1)));
				} else {
					command += "{" + length + "+}\r\n" + latin1 + "\r\n";
					commands.Add (new ImapReplayCommand (command, string.Format ("gmail.append.{0}.txt", i + 1)));
				}
			}

			commands.Add (new ImapReplayCommand ("A00000059 UID SEARCH RETURN () OR TO nsb CC nsb\r\n", "gmail.search.txt"));
			commands.Add (new ImapReplayCommand ("A00000060 UID FETCH 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODY)\r\n", "gmail.search-summary.txt"));
			commands.Add (new ImapReplayCommand ("A00000061 UID FETCH 1 (BODY.PEEK[])\r\n", "gmail.fetch.1.txt"));
			commands.Add (new ImapReplayCommand ("A00000062 UID FETCH 2 (BODY.PEEK[])\r\n", "gmail.fetch.2.txt"));
			commands.Add (new ImapReplayCommand ("A00000063 UID FETCH 3 (BODY.PEEK[])\r\n", "gmail.fetch.3.txt"));
			commands.Add (new ImapReplayCommand ("A00000064 UID FETCH 5 (BODY.PEEK[])\r\n", "gmail.fetch.5.txt"));
			commands.Add (new ImapReplayCommand ("A00000065 UID FETCH 7 (BODY.PEEK[])\r\n", "gmail.fetch.7.txt"));
			commands.Add (new ImapReplayCommand ("A00000066 UID FETCH 8 (BODY.PEEK[])\r\n", "gmail.fetch.8.txt"));
			commands.Add (new ImapReplayCommand ("A00000067 UID FETCH 9 (BODY.PEEK[])\r\n", "gmail.fetch.9.txt"));
			commands.Add (new ImapReplayCommand ("A00000068 UID FETCH 11 (BODY.PEEK[])\r\n", "gmail.fetch.11.txt"));
			commands.Add (new ImapReplayCommand ("A00000069 UID FETCH 12 (BODY.PEEK[])\r\n", "gmail.fetch.12.txt"));
			commands.Add (new ImapReplayCommand ("A00000070 UID FETCH 13 (BODY.PEEK[])\r\n", "gmail.fetch.13.txt"));
			commands.Add (new ImapReplayCommand ("A00000071 UID FETCH 14 (BODY.PEEK[])\r\n", "gmail.fetch.14.txt"));
			commands.Add (new ImapReplayCommand ("A00000072 UID FETCH 26 (BODY.PEEK[])\r\n", "gmail.fetch.26.txt"));
			commands.Add (new ImapReplayCommand ("A00000073 UID FETCH 27 (BODY.PEEK[])\r\n", "gmail.fetch.27.txt"));
			commands.Add (new ImapReplayCommand ("A00000074 UID FETCH 28 (BODY.PEEK[])\r\n", "gmail.fetch.28.txt"));
			commands.Add (new ImapReplayCommand ("A00000075 UID FETCH 29 (BODY.PEEK[])\r\n", "gmail.fetch.29.txt"));
			commands.Add (new ImapReplayCommand ("A00000076 UID FETCH 31 (BODY.PEEK[])\r\n", "gmail.fetch.31.txt"));
			commands.Add (new ImapReplayCommand ("A00000077 UID FETCH 34 (BODY.PEEK[])\r\n", "gmail.fetch.34.txt"));
			commands.Add (new ImapReplayCommand ("A00000078 UID FETCH 41 (BODY.PEEK[])\r\n", "gmail.fetch.41.txt"));
			commands.Add (new ImapReplayCommand ("A00000079 UID FETCH 42 (BODY.PEEK[])\r\n", "gmail.fetch.42.txt"));
			commands.Add (new ImapReplayCommand ("A00000080 UID FETCH 43 (BODY.PEEK[])\r\n", "gmail.fetch.43.txt"));
			commands.Add (new ImapReplayCommand ("A00000081 UID FETCH 50 (BODY.PEEK[])\r\n", "gmail.fetch.50.txt"));
			commands.Add (new ImapReplayCommand ("A00000082 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 FLAGS (\\Answered \\Seen)\r\n", "gmail.set-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000083 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -FLAGS.SILENT (\\Answered)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000084 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", "gmail.add-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000085 CHECK\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000086 UNSELECT\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000087 SUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000088 LSUB \"\" \"%\"\r\n", "gmail.lsub-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000089 UNSUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000090 CREATE UnitTests/Dummy\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000091 LIST \"\" UnitTests/Dummy\r\n", "gmail.list-unittests-dummy.txt"));
			commands.Add (new ImapReplayCommand ("A00000092 RENAME UnitTests RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000093 DELETE RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000094 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.AppendLimit.HasValue, "Expected AppendLimit to have a value");
				Assert.AreEqual (35651584, client.AppendLimit.Value, "Expected AppendLimit value to match");

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = (await personal.GetSubfoldersAsync ()).ToList ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = await personal.CreateAsync ("UnitTests", true);
				Assert.IsNotNull (created, "Expected a non-null created folder.");
				Assert.AreEqual (FolderAttributes.HasNoChildren, created.Attributes);

				Assert.IsNotNull (created.ParentFolder, "The ParentFolder property should not be null.");

				const MessageFlags ExpectedPermanentFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.UserDefined;
				const MessageFlags ExpectedAcceptedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen;
				var access = await created.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "The UnitTests folder was not opened with the expected access mode.");
				Assert.AreEqual (ExpectedPermanentFlags, created.PermanentFlags, "The PermanentFlags do not match the expected value.");
				Assert.AreEqual (ExpectedAcceptedFlags, created.AcceptedFlags, "The AcceptedFlags do not match the expected value.");

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						var message = MimeMessage.Load (stream);

						var uid = await created.AppendAsync (message, MessageFlags.Seen);
						Assert.IsTrue (uid.HasValue, "Expected a UID to be returned from folder.Append().");
						Assert.AreEqual ((uint) (i + 1), uid.Value.Id, "The UID returned from the APPEND command does not match the expected UID.");
					}
				}

				var query = SearchQuery.ToContains ("nsb").Or (SearchQuery.CcContains ("nsb"));
				var matches = await created.SearchAsync (query);

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId;
				var summaries = await created.FetchAsync (matches, items);

				foreach (var summary in summaries) {
					if (summary.UniqueId.IsValid)
						await created.GetMessageAsync (summary.UniqueId);
					else
						await created.GetMessageAsync (summary.Index);
				}

				await created.SetFlagsAsync (matches, MessageFlags.Seen | MessageFlags.Answered, false);
				await created.RemoveFlagsAsync (matches, MessageFlags.Answered, true);
				await created.AddFlagsAsync (matches, MessageFlags.Deleted, true);

				await created.CheckAsync ();

				await created.CloseAsync ();
				Assert.IsFalse (created.IsOpen, "Expected the UnitTests folder to be closed.");

				await created.SubscribeAsync ();
				Assert.IsTrue (created.IsSubscribed, "Expected IsSubscribed to be true after subscribing to the folder.");

				var subscribed = (await personal.GetSubfoldersAsync (true)).ToList ();
				Assert.IsTrue (subscribed.Contains (created), "Expected the list of subscribed folders to contain the UnitTests folder.");

				await created.UnsubscribeAsync ();
				Assert.IsFalse (created.IsSubscribed, "Expected IsSubscribed to be false after unsubscribing from the folder.");

				var dummy = await created.CreateAsync ("Dummy", true);
				bool dummyRenamed = false;
				bool renamed = false;
				bool deleted = false;

				dummy.Renamed += (sender, e) => { dummyRenamed = true; };
				created.Renamed += (sender, e) => { renamed = true; };

				await created.RenameAsync (created.ParentFolder, "RenamedUnitTests");
				Assert.AreEqual ("RenamedUnitTests", created.Name);
				Assert.AreEqual ("RenamedUnitTests", created.FullName);
				Assert.IsTrue (renamed, "Expected the Rename event to be emitted for the UnitTests folder.");

				Assert.AreEqual ("RenamedUnitTests/Dummy", dummy.FullName);
				Assert.IsTrue (dummyRenamed, "Expected the Rename event to be emitted for the UnitTests/Dummy folder.");

				created.Deleted += (sender, e) => { deleted = true; };

				await created.DeleteAsync ();
				Assert.IsTrue (deleted, "Expected the Deleted event to be emitted for the UnitTests folder.");

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public async void TestAccessControlLists ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "acl.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "acl.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETACL INBOX\r\n", "acl.getacl.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LISTRIGHTS INBOX smith\r\n", "acl.listrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 MYRIGHTS INBOX\r\n", "acl.myrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETACL INBOX smith +lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000009 SETACL INBOX smith -lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000010 SETACL INBOX smith lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000011 DELETEACL INBOX smith\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (AclInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (AclAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// GETACL INBOX
				var acl = await client.Inbox.GetAccessControlListAsync ();
				Assert.AreEqual (2, acl.Count, "The number of access controls does not match.");
				Assert.AreEqual ("Fred", acl[0].Name, "The identifier for the first access control does not match.");
				Assert.AreEqual ("rwipslxetad", acl[0].Rights.ToString (), "The access rights for the first access control does not match.");
				Assert.AreEqual ("Chris", acl[1].Name, "The identifier for the second access control does not match.");
				Assert.AreEqual ("lrswi", acl[1].Rights.ToString (), "The access rights for the second access control does not match.");

				// LISTRIGHTS INBOX smith
				Assert.Throws<ArgumentNullException> (() => client.Inbox.GetAccessRights (null));
				//Assert.Throws<ArgumentException> (() => client.Inbox.GetAccessRights (string.Empty));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.GetAccessRightsAsync (null));
				//Assert.Throws<ArgumentException> (async () => await client.Inbox.GetAccessRightsAsync (string.Empty));
				var rights = await client.Inbox.GetAccessRightsAsync ("smith");
				Assert.AreEqual ("lrswipkxtecda0123456789", rights.ToString (), "The access rights do not match for user smith.");

				// MYRIGHTS INBOX
				rights = await client.Inbox.GetMyAccessRightsAsync ();
				Assert.AreEqual ("rwiptsldaex", rights.ToString (), "My access rights do not match.");

				// SETACL INBOX smith +lrswida
				var empty = new AccessRights (string.Empty);
				rights = new AccessRights ("lrswida");
				Assert.Throws<ArgumentNullException> (() => client.Inbox.AddAccessRights (null, rights));
				//Assert.Throws<ArgumentException> (() => client.Inbox.AddAccessRights (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (() => client.Inbox.AddAccessRights ("smith", null));
				Assert.Throws<ArgumentException> (() => client.Inbox.AddAccessRights ("smith", empty));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.AddAccessRightsAsync (null, rights));
				//Assert.Throws<ArgumentException> (async () => await client.Inbox.AddAccessRightsAsync (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.AddAccessRightsAsync ("smith", null));
				Assert.Throws<ArgumentException> (async () => await client.Inbox.AddAccessRightsAsync ("smith", empty));
				await client.Inbox.AddAccessRightsAsync ("smith", rights);

				// SETACL INBOX smith -lrswida
				Assert.Throws<ArgumentNullException> (() => client.Inbox.RemoveAccessRights (null, rights));
				//Assert.Throws<ArgumentException> (() => client.Inbox.RemoveAccessRights (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (() => client.Inbox.RemoveAccessRights ("smith", null));
				Assert.Throws<ArgumentException> (() => client.Inbox.RemoveAccessRights ("smith", empty));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.RemoveAccessRightsAsync (null, rights));
				//Assert.Throws<ArgumentException> (async () => await client.Inbox.RemoveAccessRightsAsync (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.RemoveAccessRightsAsync ("smith", null));
				Assert.Throws<ArgumentException> (async () => await client.Inbox.RemoveAccessRightsAsync ("smith", empty));
				await client.Inbox.RemoveAccessRightsAsync ("smith", rights);

				// SETACL INBOX smith lrswida
				Assert.Throws<ArgumentNullException> (() => client.Inbox.SetAccessRights (null, rights));
				//Assert.Throws<ArgumentException> (() => client.Inbox.SetAccessRights (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (() => client.Inbox.SetAccessRights ("smith", null));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.SetAccessRightsAsync (null, rights));
				//Assert.Throws<ArgumentException> (async () => await client.Inbox.SetAccessRightsAsync (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.SetAccessRightsAsync ("smith", null));
				await client.Inbox.SetAccessRightsAsync ("smith", rights);

				// DELETEACL INBOX smith
				Assert.Throws<ArgumentNullException> (() => client.Inbox.RemoveAccess (null));
				//Assert.Throws<ArgumentException> (() => client.Inbox.RemoveAccess (string.Empty));
				Assert.Throws<ArgumentNullException> (async () => await client.Inbox.RemoveAccessAsync (null));
				//Assert.Throws<ArgumentException> (async () => await client.Inbox.RemoveAccessAsync (string.Empty));
				await client.Inbox.RemoveAccessAsync ("smith");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public async void TestMetadata ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "metadata.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "metadata.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETMETADATA \"\" /private/comment\r\n", "metadata.getmetadata.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 GETMETADATA \"\" (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.getmetadata-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 GETMETADATA \"\" /private/comment /shared/comment\r\n", "metadata.getmetadata-multi.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETMETADATA \"\" (/private/comment \"this is a comment\")\r\n", "metadata.setmetadata-noprivate.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 SETMETADATA \"\" (/private/comment \"this comment is too long!\")\r\n", "metadata.setmetadata-maxsize.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 SETMETADATA \"\" (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.setmetadata-toomany.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 SETMETADATA \"\" (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000012 GETMETADATA INBOX /private/comment\r\n", "metadata.inbox-getmetadata.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 GETMETADATA INBOX (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.inbox-getmetadata-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 GETMETADATA INBOX /private/comment /shared/comment\r\n", "metadata.inbox-getmetadata-multi.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 SETMETADATA INBOX (/private/comment \"this is a comment\")\r\n", "metadata.inbox-setmetadata-noprivate.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 SETMETADATA INBOX (/private/comment \"this comment is too long!\")\r\n", "metadata.inbox-setmetadata-maxsize.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 SETMETADATA INBOX (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.inbox-setmetadata-toomany.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 SETMETADATA INBOX (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				MetadataCollection metadata;
				MetadataOptions options;

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (MetadataInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (MetadataAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// GETMETADATA
				Assert.AreEqual ("this is a comment", await client.GetMetadataAsync (MetadataTag.PrivateComment), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = await client.GetMetadataAsync (options, new [] { new MetadataTag ("/private") });
				Assert.AreEqual (1, metadata.Count, "Expected 1 metadata value.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "Metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "Metadata value did not match.");
				Assert.AreEqual (2199, options.LongEntries, "LongEntries does not match.");

				metadata = await client.GetMetadataAsync (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.AreEqual (2, metadata.Count, "Expected 2 metadata values.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "First metadata tag did not match.");
				Assert.AreEqual (MetadataTag.SharedComment.Id, metadata[1].Tag.Id, "Second metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "First metadata value did not match.");
				Assert.AreEqual ("this is a shared comment", metadata[1].Value, "Second metadata value did not match.");

				// SETMETADATA
				Assert.Throws<ImapCommandException> (async () => await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a comment")
				})), "Expected NOPRIVATE RESP-CODE.");
				Assert.Throws<ImapCommandException> (async () => await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this comment is too long!")
				})), "Expected MAXSIZE RESP-CODE.");
				Assert.Throws<ImapCommandException> (async () => await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a private comment"),
					new Metadata (MetadataTag.SharedComment, "this is a shared comment"),
				})), "Expected TOOMANY RESP-CODE.");
				await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				// GETMETADATA folder
				Assert.AreEqual ("this is a comment", await inbox.GetMetadataAsync (MetadataTag.PrivateComment), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = await inbox.GetMetadataAsync (options, new [] { new MetadataTag ("/private") });
				Assert.AreEqual (1, metadata.Count, "Expected 1 metadata value.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "Metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "Metadata value did not match.");
				Assert.AreEqual (2199, options.LongEntries, "LongEntries does not match.");

				metadata = await inbox.GetMetadataAsync (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.AreEqual (2, metadata.Count, "Expected 2 metadata values.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "First metadata tag did not match.");
				Assert.AreEqual (MetadataTag.SharedComment.Id, metadata[1].Tag.Id, "Second metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "First metadata value did not match.");
				Assert.AreEqual ("this is a shared comment", metadata[1].Value, "Second metadata value did not match.");

				// SETMETADATA folder
				Assert.Throws<ImapCommandException> (async () => await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a comment")
				})), "Expected NOPRIVATE RESP-CODE.");
				Assert.Throws<ImapCommandException> (async () => await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this comment is too long!")
				})), "Expected MAXSIZE RESP-CODE.");
				Assert.Throws<ImapCommandException> (async () => await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a private comment"),
					new Metadata (MetadataTag.SharedComment, "this is a shared comment"),
				})), "Expected TOOMANY RESP-CODE.");
				await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public async void TestExtractingPrecisePangolinAttachment ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 FETCH 270 (BODY.PEEK[])\r\n", "gmail.precise-pangolin-message.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = (await personal.GetSubfoldersAsync ()).ToList ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				await client.Inbox.OpenAsync (FolderAccess.ReadOnly);

				var message = await client.Inbox.GetMessageAsync (269);

				using (var jpeg = new MemoryStream ()) {
					var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

					attachment.ContentObject.DecodeTo (jpeg);
					jpeg.Position = 0;

					using (var md5 = new MD5CryptoServiceProvider ()) {
						var md5sum = HexEncode (md5.ComputeHash (jpeg));

						Assert.AreEqual ("167a46aa81e881da2ea8a840727384d3", md5sum, "MD5 checksums do not match.");
					}
				}

				await client.DisconnectAsync (false);
			}
		}
		
		[Test]
		public async void TestMessageCount ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			// INBOX has 1 message present in this test
			commands.Add (new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.count.examine.txt"));
			// next command simulates one expunge + one new message
			commands.Add (new ImapReplayCommand ("A00000006 NOOP\r\n", "gmail.count.noop.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				
				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}
				
				var count = -1;
				
				await client.Inbox.OpenAsync (FolderAccess.ReadOnly);
				
				client.Inbox.CountChanged += delegate {
					count = client.Inbox.Count;
				};
				
				await client.NoOpAsync ();
				
				Assert.AreEqual (1, count, "Count is not correct");
			}
		}
	}
}
