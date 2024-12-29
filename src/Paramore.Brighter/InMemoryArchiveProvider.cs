﻿#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// Use this archiver will result in messages being stored in memory. Mainly useful for tests.
/// Use the <see cref="NullOutboxArchiveProvider"/> if you just want to discard and not archive 
/// </summary>
public class InMemoryArchiveProvider: IAmAnArchiveProvider
{
    public Dictionary<string, Message> ArchivedMessages { get; set; } = new();
    
    public void ArchiveMessage(Message message)
    {
        ArchivedMessages.Add(message.Id!, message);
    }

    public Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
    {
        ArchivedMessages.Add(message.Id!, message);
        return Task.CompletedTask;
    }

    public Task<string[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            ArchivedMessages.Add(message.Id!, message);
        }

        return Task.FromResult(messages.Select(m => m.Id!).ToArray());
    }
}
