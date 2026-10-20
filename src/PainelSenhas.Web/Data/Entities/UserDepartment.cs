namespace PainelSenhas.Web.Data;

/// <summary>Vínculo entre um operador e um departamento que ele pode atender. Um operador pode ter vários.</summary>
internal sealed class UserDepartment
{
    public string UserId { get; set; } = "";

    public int DepartmentId { get; set; }
}
