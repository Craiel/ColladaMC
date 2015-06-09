namespace ColladaMC
{
    using CarbonCore.Utils.Diagnostics;
    using CarbonCore.Utils.IoC;

    using ColladaMC.Contracts;
    using ColladaMC.IoC;

    public static class Entry
    {
        // -------------------------------------------------------------------
        // Public
        // -------------------------------------------------------------------
        public static void Main(string[] args)
        {
            var container = CarbonContainerBuilder.Build<ColladaMCModule>();
            container.Resolve<IColladaMinecraft>().Process();

            Profiler.TraceProfilerStatistics();
        }
    }
}
