using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Csla.Properties;

namespace Csla
{
  internal static class MethodCaller
  {
    /// <summary>
    /// Uses reflection to dynamically invoke a method
    /// if that method is implemented on the target object.
    /// </summary>
    public static object CallMethodIfImplemented(
      object obj, string method, params object[] parameters)
    {
      MethodInfo info = GetMethod(obj.GetType(), method, parameters);
      if (info != null)
        return CallMethod(obj, info, parameters);
      else
        return null;
    }

    /// <summary>
    /// Uses reflection to dynamically invoke a method,
    /// throwing an exception if it is not
    /// implemented on the target object.
    /// </summary>
    public static object CallMethod(
      object obj, string method, params object[] parameters)
    {
      MethodInfo info = GetMethod(obj.GetType(), method, parameters);
      if (info == null)
        throw new NotImplementedException(
          method + " " + Resources.MethodNotImplemented);
      return CallMethod(obj, info, parameters);
    }

    /// <summary>
    /// Uses reflection to dynamically invoke a method,
    /// throwing an exception if it is not
    /// implemented on the target object.
    /// </summary>
    public static object CallMethod(
      object obj, MethodInfo info, params object[] parameters)
    {
      // call a private method on the object
      object result;
      try
      {
        result = info.Invoke(obj, parameters);
      }
      catch (Exception e)
      {
        throw new Csla.Server.CallMethodException(
          info.Name + " " + Resources.MethodCallFailed, e.InnerException);
      }
      return result;
    }

    /// <summary>
    /// Uses reflection to locate a matching method
    /// on the target object.
    /// </summary>
    public static MethodInfo GetMethod(
      Type objectType, string method, params object[] parameters)
    {
      BindingFlags flags =
        BindingFlags.FlattenHierarchy |
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

      MethodInfo result = null;

      // try to find a strongly typed match
      if (parameters.Length > 0)
      {
        // put all param types into a list of Type
        bool paramsAllNothing = true;
        List<Type> types = new List<Type>();
        foreach (object item in parameters)
        {
          if (item == null)
            types.Add(typeof(object));
          else
          {
            types.Add(item.GetType());
            paramsAllNothing = false;
          }
        }

        if (paramsAllNothing)
        {
          // all params are null so we have
          // no type info to go on
          BindingFlags oneLevelFlags =
            BindingFlags.DeclaredOnly |
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;
          Type[] typesArray = types.ToArray();

          // walk up the inheritance hierarchy looking
          // for a method with the right number of
          // parameters
          Type currentType = objectType;
          do
          {
            MethodInfo info = currentType.GetMethod(method, oneLevelFlags);
            if (info != null)
            {
              if (info.GetParameters().Length == parameters.Length)
              {
                // got a match so use it
                result = info;
                break;
              }
            }
            currentType = currentType.BaseType;
          } while (currentType != null);
        }
        else
        {
          // at least one param has a real value
          // so search for a strongly typed match
          //result = objectType.GetMethod(method, flags, null,
          //  CallingConventions.Any, types.ToArray(), null);
          result = FindMethod(objectType, method, types.ToArray());
        }
      }

      // no strongly typed match found, get default
      if (result == null)
      {
        try
        { result = objectType.GetMethod(method, flags); }
        catch (AmbiguousMatchException)
        {
          MethodInfo[] methods = objectType.GetMethods();
          foreach (MethodInfo m in methods)
            if (m.Name == method && m.GetParameters().Length == parameters.Length)
            {
              result = m;
              break;
            }
          if (result == null)
            throw;
        }
      }
      return result;
    }

    /// <summary>
    /// Returns a business object type based on
    /// the supplied criteria object.
    /// </summary>
    public static Type GetObjectType(object criteria)
    {
      if (criteria.GetType().IsSubclassOf(typeof(CriteriaBase)))
      {
        // get the type of the actual business object
        // from CriteriaBase 
        return ((CriteriaBase)criteria).ObjectType;
      }
      else
      {
        // get the type of the actual business object
        // based on the nested class scheme in the book
        return criteria.GetType().DeclaringType;
      }
    }

    /// <summary>
    /// Returns information about the specified
    /// method, even if the parameter types are
    /// generic and are located in an abstract
    /// generic base class.
    /// </summary>
    public static MethodInfo FindMethod(Type objType, string method, Type[] types)
    {
      if (objType == null) return null;

      BindingFlags oneLevelFlags =
            BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;
      MethodInfo m = objType.GetMethod(method, oneLevelFlags);

      if (m != null)
      {
        ParameterInfo[] pars = m.GetParameters();
        if (pars.Length == 0 && types.Length == 0)
          return m;     // no parameters is a match

        if (pars.Length == types.Length)
        {
          //equal number of parameters, check if the same type.
          bool match = true;

          for (int i = 0; i < pars.Length; i++)
          {
            if (pars[i].ParameterType != types[i])
            {
              match = false;
              break;
            }
          }
          if (match) //found method
            return m;

        }
      }
      //not found, get next level down
      return FindMethod(objType.BaseType, method, types);
    }
  }
}
