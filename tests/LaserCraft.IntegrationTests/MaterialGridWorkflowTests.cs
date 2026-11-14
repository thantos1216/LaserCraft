using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;
public sealed class MaterialGridWorkflowTests
{
    [Fact]public void GeneratedGridKeepsSettingsAndGroupedLayoutAfterSaveMoveAndUngroup()
    {
        var grid=MaterialTests.Create(430,430,new(Rows:2,Columns:3,RowMinimum:10,RowMaximum:30,ColumnMinimum:300,ColumnMaximum:900,Passes:3,Mode:CutMode.Fill)).Document;
        var path=Path.Combine(Path.GetTempPath(),"LaserCraft-grid-"+Guid.NewGuid()+".lcraft");
        try
        {
            ProjectFile.Save(path,grid);var loaded=ProjectFile.Load(path);Assert.Equal(grid.Layers,loaded.Layers);Assert.Equal(grid.Shapes.Select(s=>s.GroupId),loaded.Shapes.Select(s=>s.GroupId));
            var selected=ShapeGroups.Select(loaded,new HashSet<Guid>(),loaded.Shapes[0].Id,false);Assert.Equal(6,selected.Count);
            var moved=ShapeEditing.Transform(loaded,selected,s=>s with {X=s.X+17,Y=s.Y+23});
            for(int i=0;i<6;i++){Assert.Equal(17,moved.Shapes[i].X-loaded.Shapes[i].X,8);Assert.Equal(23,moved.Shapes[i].Y-loaded.Shapes[i].Y,8);}
            var ungrouped=ShapeGroups.Ungroup(moved,selected).Document;Assert.All(ungrouped.Shapes,s=>Assert.Null(s.GroupId));Assert.Equal(moved.Layers,ungrouped.Layers);
            var before=ToolpathPlanner.Build(loaded);var after=ToolpathPlanner.Build(ungrouped);Assert.Equal(before.CutLength,after.CutLength,6);Assert.Contains(after.Segments,s=>!s.Travel&&s.Power==10);Assert.Contains(after.Segments,s=>!s.Travel&&s.Power==30);
        }
        finally{File.Delete(path);}
    }
}
