namespace ColladaMC.IoC
{
    using CarbonCore.GrammarParser.IoC;
    using CarbonCore.Utils.IoC;
    using CarbonCore.UtilsCommandLine.IoC;

    using ColladaMC.Contracts;

    [DependsOnModule(typeof(UtilsModule))]
    [DependsOnModule(typeof(UtilsCommandLineModule))]
    [DependsOnModule(typeof(GrammarParserModule))]
    public class ColladaMCModule : CarbonModule
    {
        public ColladaMCModule()
        {
            this.For<IColladaMinecraft>().Use<ColladaMinecraft>();
        }
    }
}