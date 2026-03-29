using System.Data;
using Dapper;

namespace OnFlight.Core.Data;

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value) => Guid.Parse(value.ToString()!);

    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }
}

public class NullableGuidTypeHandler : SqlMapper.TypeHandler<Guid?>
{
    public override Guid? Parse(object value)
        => value is null or DBNull ? null : Guid.Parse(value.ToString()!);

    public override void SetValue(IDbDataParameter parameter, Guid? value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value?.ToString() ?? (object)DBNull.Value;
    }
}

public class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value) => DateTime.Parse(value.ToString()!).ToUniversalTime();

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString("o");
    }
}

public static class DapperConfig
{
    public static void RegisterTypeHandlers()
    {
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        SqlMapper.AddTypeHandler(new NullableGuidTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
    }
}
