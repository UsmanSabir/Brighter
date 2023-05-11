﻿using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly IAmARelationalDbConnectionProvider _connectionProvider; 

        public AddPersonHandlerAsync(IAmARelationalDbConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default)
        {
            using (var connection = await _connectionProvider.GetConnectionAsync(cancellationToken))
            {
                await connection.InsertAsync(new Person(addPerson.Name));
            }

            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
