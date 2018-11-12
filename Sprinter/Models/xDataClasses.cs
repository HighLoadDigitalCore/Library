namespace Sprinter.Models
{
    partial class DB
    {
        partial void OnCreated()
        {
            this.CommandTimeout = 3600;
        }
    }
}
