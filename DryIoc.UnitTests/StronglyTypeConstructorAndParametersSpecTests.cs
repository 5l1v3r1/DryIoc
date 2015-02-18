﻿using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace DryIoc.UnitTests
{
    [TestFixture]
    public class StronglyTypeConstructorAndParametersSpecTests
    {
        [Test]
        public void Specify_default_constructor_without_reflection()
        {
            var container = new Container();

            container.Register<Burger>(
                with: Construct.Of(_ => new Burger()));

            var burger = container.Resolve<Burger>();
            Assert.That(burger.Cheese, Is.Null);
        }

        [Test]
        public void Specify_constructor_with_params_without_reflection()
        {
            var container = new Container();

            container.Register<Burger>(with: Construct.Of(_ => new Burger(default(ICheese))));
            container.Register<ICheese, BlueCheese>();

            var burger = container.Resolve<Burger>();
            Assert.That(burger.Cheese, Is.Not.Null);
        }

        [Test]
        public void Specify_parameter_ifUnresolved_behavior_without_reflection()
        {
            var container = new Container();

            container.Register<Burger>(
                with: Construct.Of(p => new Burger(p.Of<ICheese>(IfUnresolved.ReturnDefault))));

            var burger = container.Resolve<Burger>();
            Assert.That(burger.Cheese, Is.Null);
        }

        [Test]
        public void Specify_primitive_parameter_value_directly()
        {
            var container = new Container();

            container.Register<Burger>(with: Construct.Of(p => new Burger("King", p.Of<ICheese>())));
            container.Register<ICheese, BlueCheese>();

            var burger = container.Resolve<Burger>();
            Assert.AreEqual("King", burger.Name);
        }

        [Test]
        public void Specify_service_key_and_required_service_type_for_parameter()
        {
            var container = new Container();

            container.Register<BlueCheese>(named: "a");
            container.Register<Burger>(
                with: Construct.Of(p => new Burger("King", p.Of<BlueCheese>("a"))));

            var burger = container.Resolve<Burger>();
            Assert.AreEqual("King", burger.Name);
        }

        [Test]
        public void Specify_static_factory_method()
        {
            var container = new Container();

            container.Register<Burger>(with: Construct.Of(p => Burger.Create()));

            Assert.NotNull(container.Resolve<Burger>());
        }

        internal interface ICheese { }

        internal class BlueCheese : ICheese { }

        internal class Burger
        {
            public readonly string Name;

            public ICheese Cheese { get; private set; }

            public Burger() { }

            public Burger(ICheese cheese)
            {
                Cheese = cheese;
            }

            public Burger(string name, ICheese cheese)
            {
                Name = name;
                Cheese = cheese;
            }

            public static Burger Create()
            {
                return new Burger();
            }
        }
    }

    public static class Construct
    {
        public static InjectionRules Of<TImpl>(Expression<Func<Params, TImpl>> methodOrCtorCallExpression)
        {
            MethodBase methodOrCtor;
            ReadOnlyCollection<Expression> argExprs;

            var callExpr = methodOrCtorCallExpression.Body;
            var newExpr = callExpr as NewExpression;
            if (newExpr != null)
            {
                argExprs = newExpr.Arguments;
                methodOrCtor = newExpr.Constructor;
            }
            else
            {
                var methodCallExpr = callExpr as MethodCallExpression;
                if (methodCallExpr != null)
                {
                    argExprs = methodCallExpr.Arguments;
                    methodOrCtor = methodCallExpr.Method;
                }
                else return Throw.Instead<InjectionRules>(Error.Of("Not supported expression"));
            }

            var pars = methodOrCtor.GetParameters();
            var parameters = Parameters.Of;
            if (argExprs.Count != 0)
            {
                for (var i = 0; i < argExprs.Count; i++)
                {
                    var par = pars[i];
                    var arg = argExprs[i] as MethodCallExpression;
                    if (arg != null && arg.Method.DeclaringType == typeof(Params))
                    {
                        var requiredServiceType = arg.Method.GetGenericArguments()[0];
                        if (requiredServiceType == par.ParameterType)
                            requiredServiceType = null;
                        
                        var serviceKey = default(object);
                        var ifUnresolved = IfUnresolved.Throw;

                        var settings = arg.Arguments;
                        for (var j = 0; j < settings.Count; j++)
                        {
                            var setting = settings[j] as ConstantExpression;
                            if (setting != null)
                            {
                                if (setting.Type == typeof(IfUnresolved))
                                    ifUnresolved = (IfUnresolved)setting.Value;
                                else // service key
                                    serviceKey = setting.Value;
                            }
                        }

                        parameters = parameters.Condition(par.Equals, requiredServiceType, serviceKey, ifUnresolved);
                    }
                    else
                    {
                        var argValue = argExprs[i] as ConstantExpression;
                        if (argValue != null && argValue.Type.IsPrimitive())
                        {
                            parameters = parameters.Name(par.Name, argValue.Value);
                        }
                    }
                }
            }

            return InjectionRules.With(request => FactoryMethod.Of(methodOrCtor), parameters);
        }
    }

    public sealed class Params
    {
        public static readonly Params Default = new Params();

        public T Of<T>() { return default(T); }

        public T Of<T>(IfUnresolved ifUnresolved) { return default(T); }
        
        public T Of<T>(object serviceKey) { return default(T); }
        
        public T Of<T>(object serviceKey, IfUnresolved ifUnresolved) { return default(T); }

        private Params() { }
    }
}
