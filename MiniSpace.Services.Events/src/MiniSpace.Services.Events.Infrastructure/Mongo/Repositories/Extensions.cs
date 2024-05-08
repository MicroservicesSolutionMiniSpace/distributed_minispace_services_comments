﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MiniSpace.Services.Events.Core.Entities;
using MiniSpace.Services.Events.Infrastructure.Mongo.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MiniSpace.Services.Events.Infrastructure.Mongo.Repositories
{
    public static class Extensions
    {
        private static readonly FilterDefinitionBuilder<EventDocument> FilterDefinitionBuilder = Builders<EventDocument>.Filter;
        public static async Task<(int totalPages, int totalElements, IReadOnlyList<TDocument> data)> AggregateByPage<TDocument>(
            this IMongoCollection<TDocument> collection,
            FilterDefinition<TDocument> filterDefinition,
            SortDefinition<TDocument> sortDefinition,
            int page,
            int pageSize)
        {
            var countFacet = AggregateFacet.Create("count",
                PipelineDefinition<TDocument, AggregateCountResult>.Create(new[]
                {
                    PipelineStageDefinitionBuilder.Count<TDocument>()
                }));

            var dataFacet = AggregateFacet.Create("data",
                PipelineDefinition<TDocument, TDocument>.Create(new[]
                {
                    PipelineStageDefinitionBuilder.Sort(sortDefinition),
                    PipelineStageDefinitionBuilder.Skip<TDocument>((page - 1) * pageSize),
                    PipelineStageDefinitionBuilder.Limit<TDocument>(pageSize),
                }));


            var aggregation = await collection.Aggregate()
                .Match(filterDefinition)
                .Facet(countFacet, dataFacet)
                .ToListAsync();

            var count = aggregation.First()
                .Facets.First(x => x.Name == "count")
                .Output<AggregateCountResult>()
                ?.FirstOrDefault()
                ?.Count;

            if (count == null)
            {
                return (0, 0, Array.Empty<TDocument>());
            }
            var totalPages = (int)Math.Ceiling((double)count / pageSize);

            var data = aggregation.First()
                .Facets.First(x => x.Name == "data")
                .Output<TDocument>();

            return (totalPages, (int)count, data);
        }
        
        public static FilterDefinition<EventDocument> ToFilterDefinition(string name, DateTime dateFrom, 
            DateTime dateTo, IEnumerable<Guid> eventIds = null)
        {
            var filterDefinition = FilterDefinitionBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(name))
            {
                filterDefinition &= FilterDefinitionBuilder.Regex(x => x.Name,
                    new BsonRegularExpression(new Regex($".*{name}.*", RegexOptions.IgnoreCase)));
            }

            if (dateFrom != DateTime.MinValue)
            {
                filterDefinition &= FilterDefinitionBuilder.Gte(x => x.StartDate, dateFrom);
            }

            if (dateTo != DateTime.MinValue)
            {
                filterDefinition &= FilterDefinitionBuilder.Lte(x => x.EndDate, dateTo);
            }

            if (eventIds != null)
            {
                filterDefinition &= FilterDefinitionBuilder.In(x => x.Id, eventIds);
            }

            return filterDefinition;
        }
        
        public static FilterDefinition<EventDocument> AddOrganizerNameFilter (
            this FilterDefinition<EventDocument> filterDefinition, string organizer)
        {
            if (!string.IsNullOrWhiteSpace(organizer))
            {   
                filterDefinition &= FilterDefinitionBuilder.Regex(x => x.Organizer.OrganizationName, 
                    new BsonRegularExpression(new Regex($".*{organizer}.*", RegexOptions.IgnoreCase)));
            }

            return filterDefinition;
        }
        
        public static FilterDefinition<EventDocument> AddOrganizerIdFilter (this FilterDefinition<EventDocument> filterDefinition, Guid organizerId)
        {
            filterDefinition &= FilterDefinitionBuilder.Eq(x => x.Organizer.Id, organizerId);
            return filterDefinition;
        }
        
        public static FilterDefinition<EventDocument> AddCategoryFilter (this FilterDefinition<EventDocument> filterDefinition, Category? category)
        {
            if (category != null)
            {
                filterDefinition &= FilterDefinitionBuilder.Eq(x => x.Category, category);
            }

            return filterDefinition;
        }
        
        public static FilterDefinition<EventDocument> AddStateFilter (this FilterDefinition<EventDocument> filterDefinition, State? state)
        {
            if (state != null)
            {
                filterDefinition &= FilterDefinitionBuilder.Eq(x => x.State, state);
            }

            return filterDefinition;
        }
        
        public static FilterDefinition<EventDocument> AddRestrictedStateFilter (this FilterDefinition<EventDocument> filterDefinition, State? state)
        {
            if (state != null)
            {
                filterDefinition &= FilterDefinitionBuilder.Eq(x => x.State, state);
            }
            else
            {
                filterDefinition &= FilterDefinitionBuilder.In(x => x.State, new[] { State.Published, State.Archived });
            }

            return filterDefinition;
        }
        
        public static FilterDefinition<EventDocument> AddFriendsFilter (this FilterDefinition<EventDocument> filterDefinition,
            IEnumerable<Guid> friendsEnumerable, EventEngagementType? friendsEngagementType)
        {
            var friends = friendsEnumerable.ToList();
            if (friends.Count == 0)
            {
                return filterDefinition;
            }
            
            if (friendsEngagementType != null)  
            {
                filterDefinition &= friendsEngagementType == EventEngagementType.InterestedIn 
                    ? FilterDefinitionBuilder.ElemMatch(x => x.InterestedStudents, s => friends.Contains(s.StudentId))
                    : FilterDefinitionBuilder.ElemMatch(x => x.SignedUpStudents, s => friends.Contains(s.StudentId));
            }
            else
            {
                var interestedFilter = FilterDefinitionBuilder.ElemMatch(x => x.InterestedStudents, s => friends.Contains(s.StudentId));
                var signedUpFilter = FilterDefinitionBuilder.ElemMatch(x => x.SignedUpStudents, s => friends.Contains(s.StudentId));
                filterDefinition &= FilterDefinitionBuilder.Or(interestedFilter, signedUpFilter);
            }

            return filterDefinition;
        }
        
        public static SortDefinition<EventDocument> ToSortDefinition(IEnumerable<string> sortByArguments, string direction)
        {
            var sort = sortByArguments.ToList();
            if(sort.Count == 0)
            {
                sort.Add("StartDate");
            }
            var sortDefinitionBuilder = Builders<EventDocument>.Sort;
            var sortStateDefinition = new[] { sortDefinitionBuilder.Descending("State") };
            var sortDefinition = sort
                .Select(sortBy => direction == "asc"
                    ? sortDefinitionBuilder.Ascending(sortBy)
                    : sortDefinitionBuilder.Descending(sortBy));
            var sortCombined = sortDefinitionBuilder.Combine(sortStateDefinition.Concat(sortDefinition));

            return sortCombined;
        }
    }
}