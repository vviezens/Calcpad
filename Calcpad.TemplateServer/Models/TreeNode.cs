namespace Calcpad.TemplateServer.Models
{
    public class TreeNode
    {
        public string Name { get; set; } = string.Empty;
        public List<TreeNode> Children { get; set; } = new();

        public TreeNode(string name)
        {
            Name = name;
            Children = new List<TreeNode>();
        }
    }
}
