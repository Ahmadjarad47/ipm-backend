using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Abstractions.Data;

/// <summary>
/// The tables handlers read and write, without knowing PostgreSQL. Each part adds its <c>DbSet</c>s here.
/// Writes are committed with <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IApplicationDbContext
{
    // Part 01: identity.
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    // Part 02: work item type settings.
    DbSet<WorkItemType> WorkItemTypes { get; }

    DbSet<RelationType> RelationTypes { get; }

    // Part 03: statuses and workflows.
    DbSet<Status> Statuses { get; }

    DbSet<Workflow> Workflows { get; }

    // Part 04: projects.
    DbSet<Project> Projects { get; }

    DbSet<ProjectMember> ProjectMembers { get; }

    DbSet<ProjectStatusChange> ProjectStatusChanges { get; }

    DbSet<ProjectTemplate> ProjectTemplates { get; }

    // Part 05: work items.
    DbSet<WorkItem> WorkItems { get; }

    DbSet<WorkItemStatusChange> WorkItemStatusChanges { get; }

    // Part 05b: relations and comments.
    DbSet<WorkItemRelation> WorkItemRelations { get; }

    DbSet<Comment> Comments { get; }
}
