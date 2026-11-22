using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members", Schemas.Projects);
        builder.HasKey(m => m.Id);

        // R7: a user is in a project once.
        builder.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();
        // "My projects" and "what can this user do" start from the user.
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.RoleId);

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
