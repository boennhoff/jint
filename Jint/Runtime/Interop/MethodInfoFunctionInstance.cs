﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    public sealed class MethodInfoFunctionInstance : FunctionInstance
    {
        private readonly MethodInfo[] _methods;

        public MethodInfoFunctionInstance(Engine engine, MethodInfo[] methods)
            : base(engine, "Function", null, null, false)
        {
            _methods = methods;
            Prototype = engine.Function.PrototypeObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Invoke(_methods, thisObject, arguments);
        }

        public JsValue Invoke(MethodInfo[] methodInfos, JsValue thisObject, JsValue[] jsArguments)
        {
            JsValue[] ArgumentProvider(MethodInfo method, bool hasParams, bool isExtension)
            {
                // in case we work with an extension method, add thisObject as first argument
                var jsArgs = jsArguments;
                if (isExtension) {
                    var args = new System.Collections.Generic.List<JsValue> {thisObject};
                    foreach (var argument in jsArguments) args.Add(argument);
                    jsArgs = args.ToArray();
                }
                
                return hasParams ? ProcessParamsArrays(jsArgs, method) : jsArgs;
            }

            var converter = Engine.ClrTypeConverter;

            foreach (var tuple in TypeConverter.FindBestMatch(_engine, methodInfos, ArgumentProvider))
            {
                var method = tuple.Item1;
                var arguments = tuple.Item2;

                var parameters = new object[arguments.Length];
                var methodParameters = method.GetParameters();
                var argumentsMatch = true;

                for (var i = 0; i < arguments.Length; i++)
                {
                    var parameterType = methodParameters[i].ParameterType;

                    if (typeof(JsValue).IsAssignableFrom(parameterType))
                    {
                        parameters[i] = arguments[i];
                    }
                    else if (parameterType == typeof(JsValue[]) && arguments[i].IsArray())
                    {
                        // Handle specific case of F(params JsValue[])

                        var arrayInstance = arguments[i].AsArray();
                        var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                        var result = new JsValue[len];
                        for (uint k = 0; k < len; k++)
                        {
                            result[k] = arrayInstance.TryGetValue(k, out var value) ? value : JsValue.Undefined;
                        }
                        parameters[i] = result;
                    }
                    else
                    {
                        if (!converter.TryConvert(arguments[i].ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
                        {
                            argumentsMatch = false;
                            break;
                        }

                        if (parameters[i] is LambdaExpression lambdaExpression)
                        {
                            parameters[i] = lambdaExpression.Compile();
                        }
                    }
                }

                if (!argumentsMatch)
                {
                    continue;
                }

                // todo: cache method info
                try
                {
                    return FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters));
                }
                catch (TargetInvocationException exception)
                {
                    var meaningfulException = exception.InnerException ?? exception;
                    var handler = Engine.Options._ClrExceptionsHandler;

                    if (handler != null && handler(meaningfulException))
                    {
                        ExceptionHelper.ThrowError(_engine, meaningfulException.Message);
                    }

                    throw meaningfulException;
                }
            }

            ExceptionHelper.ThrowTypeError(_engine, "No public methods with the specified arguments were found.");
            return null;
        }

        /// <summary>
        /// Reduces a flat list of parameters to a params array, if needed
        /// </summary>
        private JsValue[] ProcessParamsArrays(JsValue[] jsArguments, MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();

            var nonParamsArgumentsCount = parameters.Length - 1;
            if (jsArguments.Length < nonParamsArgumentsCount)
                return jsArguments;

            var argsToTransform = jsArguments.Skip(nonParamsArgumentsCount);

            if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
                return jsArguments;

            var jsArray = Engine.Array.Construct(Arguments.Empty);
            Engine.Array.PrototypeObject.Push(jsArray, argsToTransform);

            var newArgumentsCollection = new JsValue[nonParamsArgumentsCount + 1];
            for (var j = 0; j < nonParamsArgumentsCount; ++j)
            {
                newArgumentsCollection[j] = jsArguments[j];
            }

            newArgumentsCollection[nonParamsArgumentsCount] = jsArray;
            return newArgumentsCollection;
        }

    }
}
