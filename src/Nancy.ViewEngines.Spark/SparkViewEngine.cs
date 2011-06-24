﻿namespace Nancy.ViewEngines.Spark
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Dynamic;
    using System.IO;
    using global::Spark;
    using global::Spark.FileSystem;
    using Nancy.ViewEngines.Spark.Descriptors;

    /// <summary>
    /// View engine for rendering spark views.
    /// </summary>
    public class SparkViewEngine : IViewEngine
    {
        private readonly IDescriptorBuilder descriptorBuilder;
        private readonly ISparkViewEngine engine;        

        /// <summary>
        /// Initializes a new instance of the <see cref="SparkViewEngine"/> class.
        /// </summary>
        public SparkViewEngine() : this((ISparkSettings) ConfigurationManager.GetSection("spark") ?? new SparkSettings())
        {
        }

        public SparkViewEngine(ISparkSettings settings)
        {
            if (String.IsNullOrWhiteSpace(settings.PageBaseType)) 
            {
              settings.PageBaseType = typeof (NancySparkView).FullName;
            }
            this.engine = new global::Spark.SparkViewEngine(settings);
            this.descriptorBuilder = new DefaultDescriptorBuilder(this.engine);
        }

        /// <summary>
        /// Gets the extensions file extensions that are supported by the view engine.
        /// </summary>
        /// <value>An <see cref="IEnumerable{T}"/> instance containing the extensions.</value>
        /// <remarks>The extensions should not have a leading dot in the name.</remarks>
        public IEnumerable<string> Extensions
        {
            get { yield return "spark"; }
        }

        private SparkViewEngineResult CreateView<TModel>(ViewLocationResult viewLocationResult, TModel model, IRenderContext renderContext)
        {
            var result = this.LocateView(
                Path.GetDirectoryName(viewLocationResult.Location), 
                Path.GetFileNameWithoutExtension(viewLocationResult.Name),
                viewLocationResult,
                renderContext);

            var viewWithModel = result.View;

            if (viewWithModel != null)
            {
                viewWithModel.SetModel(model);
            }

            return result;
        }

        private static InMemoryViewFolder GetMemoryViewMap(IEnumerable<ViewLocationResult> viewLocationResults)
        {
            var memoryViewMap = new InMemoryViewFolder();
            foreach (var viewLocationResult in viewLocationResults)
            {
                memoryViewMap.Add(viewLocationResult.Location, viewLocationResult.Contents.Invoke().ReadToEnd());
            }
            return memoryViewMap;
        }

        private SparkViewEngineResult LocateView(string viewPath, string viewName, ViewLocationResult viewLocationResult, IRenderContext renderContext)
        {
            var searchedLocations = new List<string>();

            var descriptorParams = new BuildDescriptorParams(viewPath, viewName, null, true, null);

            var descriptor = this.descriptorBuilder.BuildDescriptor(
                descriptorParams,
                searchedLocations);

            if (descriptor == null)
            {
                return new SparkViewEngineResult(searchedLocations);
            }

            var entry = renderContext.ViewCache.GetOrAdd(
                viewLocationResult, 
                x => this.engine.CreateEntry(descriptor));

            return new SparkViewEngineResult(
                entry.CreateInstance() as NancySparkView);
        }

        public void Initialize(ViewEngineStartupContext viewEngineStartupContext)
        {
            this.engine.ViewFolder = GetMemoryViewMap(viewEngineStartupContext.ViewLocationResults);
        }

        public Action<Stream> RenderView(ViewLocationResult viewLocationResult, dynamic model, IRenderContext renderContext)
        {
            return stream =>
            {
                SparkViewEngineResult sparkViewEngineResult =
                    this.CreateView(viewLocationResult, model ?? new ExpandoObject(), renderContext);

                var writer =
                    new StreamWriter(stream);

                sparkViewEngineResult.View.Writer = writer;
                sparkViewEngineResult.View.Model = model;
                sparkViewEngineResult.View.Execute();

                writer.Flush();
            };
        }
    }
}