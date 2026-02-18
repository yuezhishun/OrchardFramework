# 通过模板创建多模块程序

## 安装模板

~~~
dotnet new install OrchardCore.ProjectTemplates
~~~

## 1、通过命令行创建项目

### 创建MVC项目

~~~
dotnet new ocmvc -n OrchardCore.Mvc.Web
~~~

### 创建模块项目

~~~
dotnet new ocmodulemvc -n OrchardCore.Mvc.HelloWorld
~~~

### 添加引用

~~~
dotnet add OrchardCore.Mvc.Web reference OrchardCore.Mvc.HelloWorld
~~~

### 创建解决方案

~~~
dotnet new sln -n OrchardCore.Mvc
dotnet sln add OrchardCore.Mvc.Web\OrchardCore.Mvc.Web.csproj
dotnet sln add OrchardCore.Mvc.HelloWorld\OrchardCore.Mvc.HelloWorld.csproj
~~~

### 打开项目，在模块中添加代码：

注：areaName必须和模块名称一致，否则不会生效

~~~csharp
routes.MapAreaControllerRoute(
    name: "Home",
    areaName: "OrchardCore.Mvc.HelloWorld",
    pattern: "",
    defaults: new { controller = "Home", action = "Index" }
);
~~~

### 启动应用程序

通过VS打开 或者使用命令

~~~
dotnet run --project .\OrchardCore.Mvc.Web\OrchardCore.Mvc.Web.csproj
~~~

## 2、通过VS创建项目

前提：安装了OrchardCore项目模板



请求路由：

当controller上不设置router时(不能使用Swagger)，完整URL是：

http://localhost:8000/租户/模块/controller/action

当controller上设置固定router时，完整URL是：

http://localhost:8000/租户/自定义router

