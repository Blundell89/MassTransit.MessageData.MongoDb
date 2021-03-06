﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LiberisLabs.MassTransit.MessageData.MongoDb.Helpers;
using MassTransit.MessageData;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace LiberisLabs.MassTransit.MessageData.MongoDb
{
    public class MongoMessageDataRepository : IMessageDataRepository
    {
        private readonly IMongoMessageUriResolver _mongoMessageUriResolver;
        private readonly IGridFSBucket _gridFsBucket;
        private readonly IFileNameCreator _randomFileNameCreator;

        public MongoMessageDataRepository(string connectionString, string database)
            :this(new MongoMessageUriResolver(), new GridFSBucket(new MongoClient(connectionString).GetDatabase(database)), new RandomFileNameCreator())
        {
            
        }


        public MongoMessageDataRepository(MongoUrl mongoUrl)
            : this(mongoUrl.Url, mongoUrl.DatabaseName)
        {

        }

        public MongoMessageDataRepository(IMongoMessageUriResolver mongoMessageUriResolver, IGridFSBucket gridFsBucket, IFileNameCreator randomFileNameCreator)
        {
            _mongoMessageUriResolver = mongoMessageUriResolver;
            _gridFsBucket = gridFsBucket;
            _randomFileNameCreator = randomFileNameCreator;
        }

        public async Task<Stream> Get(Uri address, CancellationToken cancellationToken = new CancellationToken())
        {
            var id = _mongoMessageUriResolver.Resolve(address);

            return await _gridFsBucket.OpenDownloadStreamAsync(id, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Uri> Put(Stream stream, TimeSpan? timeToLive = null, CancellationToken cancellationToken = new CancellationToken())
        {
            var options = BuildGridFSUploadOptions(timeToLive);

            var id = await _gridFsBucket.UploadFromStreamAsync(_randomFileNameCreator.CreateFileName(), stream, options, cancellationToken)
                                        .ConfigureAwait(false);

            return _mongoMessageUriResolver.Resolve(id);
        }

        private GridFSUploadOptions BuildGridFSUploadOptions(TimeSpan? timeToLive)
        {
            var metadata = new BsonDocument();

            if (timeToLive.HasValue)
            {
                metadata["expiration"] = SystemDateTime.UtcNow.Add(timeToLive.Value);
            }

            return new GridFSUploadOptions
            {
                Metadata = metadata
            };
        }
    }
}