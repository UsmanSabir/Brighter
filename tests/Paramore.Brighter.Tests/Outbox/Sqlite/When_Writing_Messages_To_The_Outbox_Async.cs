#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Tests.Outbox.Sqlite
{
    [Trait("Category", "Sqlite")]
    [Collection("Sqlite OutBox")]
    public class SqlOutboxWritngMessagesAsyncTests : IDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteOutbox _sSqlOutbox;
        private Message _message2;
        private Message _messageEarliest;
        private Message _messageLatest;
        private IList<Message> _retrievedMessages;

        public SqlOutboxWritngMessagesAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sSqlOutbox = new SqliteOutbox(new SqliteOutboxConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName_Messages));
        }

        [Fact]
        public async Task When_Writing_Messages_To_The_Outbox_Async()
        {
            await SetUpMessagesAsync();

            _retrievedMessages = await _sSqlOutbox.GetAsync();

            //_should_read_first_message_last_from_the__outbox
            _retrievedMessages.Last().Id.Should().Be(_messageEarliest.Id);
            //_should_read_last_message_first_from_the__outbox
            _retrievedMessages.First().Id.Should().Be(_messageLatest.Id);
            //_should_read_the_messages_from_the__outbox
            _retrievedMessages.Should().HaveCount(3);
        }

        private async Task SetUpMessagesAsync()
        {
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            await _sSqlOutbox.AddAsync(_messageEarliest);

            _message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            await _sSqlOutbox.AddAsync(_message2);

            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            await _sSqlOutbox.AddAsync(_messageLatest);
        }

        private void Release()
        {
           _sqliteTestHelper.CleanUpDb();
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Release();
        }

        ~SqlOutboxWritngMessagesAsyncTests()
        {
            Release();
        }
    }
}
