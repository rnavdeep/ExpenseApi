using Amazon.S3;
using Amazon.Textract;

public static class AwsConfig
{
    public static void ConfigureAwsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDefaultAWSOptions(configuration.GetAWSOptions("AWS"));
        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonTextract>();
    }
}
