using Avalonia.Collections;
using Avalonia.Media;
using JuliaSynth.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// Design files store type names ($type) so nodes come back as the right subclass. A file can name
    /// any type, and some .NET types do harmful things when created, so opening a file from someone
    /// else must only ever create the types a design is made of.
    /// </summary>
    public sealed class DesignSerializationBinder : ISerializationBinder
    {
        private static readonly HashSet<Type> AllowedTypes = [
            typeof(MainViewModel.DesignDocument),
            typeof(AvaloniaList<Node>),
            typeof(AvaloniaList<Connection>),
            typeof(ObservableCollection<Parameter>),
            typeof(Node),
            typeof(Float),
            typeof(Integer),
            typeof(UnsignedInteger),
            typeof(Function),
            typeof(Parameter),
            typeof(Connection),
            typeof(Color),
        ];

        private readonly DefaultSerializationBinder _binder = new();

        public Type BindToType(string? assemblyName, string typeName)
        {
            Type type = _binder.BindToType(assemblyName, typeName);
            return AllowedTypes.Contains(type)
                ? type
                : throw new JsonSerializationException($"A design file can't contain '{typeName}'.");
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            _binder.BindToName(serializedType, out assemblyName, out typeName);
        }
    }
}
