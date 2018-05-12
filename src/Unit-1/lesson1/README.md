# Lesson 1.1: Actors and the `ActorSystem` | Lesson 1.1: Акторы и `ActorSystem`

> Here we go! Welcome to lesson 1.

И так, мы начинаем. Добро пожаловать на урок 1.

> In this lesson, you will make your first actors and be introduced to the fundamentals of [Akka.NET](http://getakka.net/).

В рамках данного урока, вы напишите свой первый актор и познакомитесь с основами [Akka.NET](http://getakka.net/) и [Akkling](https://github.com/Horusiath/Akkling) (тут есть вики).

## Key concepts / background | Ключевые концепции 

> In this first lesson, you will learn the basics by creating a console app with your first actor system and simple actors within it.

Во время первого урока, вы узнаете основы создав консольное приложение с вашей первой системой акторов и простыми акторами внутри нее.

> We will be creating two actors, one to read from the console, and one to write to it after doing some basic processing.

Мы создадим два актора. Один, для чтения из консоли, другой для вывода в нее после выполнения некоторой примитивной обработки.

### What is an actor? | Что есть актор?
> An "actor" is really just an analog for a human participant in a system. It's an entity, an object, that can do things and communicate.

> "Актор" (да простят меня сторонники иной терминологии) это просто аналог человека действующего в системе. Это сущность, объект, который может совершать ряд действий и коммуницировать.

>> We're going to assume that you're familiar with object-oriented programming (OOP). The actor model is very similar to object-oriented programming (OOP) - just like how everything is an object in OOP, in the actor model ***everything is an actor***.

> Мы будем считать, что знакомы с объектно ориентированным программированием (ООП) (хотя я попробую реализовывать все в функциональном стиле). Акторная модель очень похожа на объектно ориентированное программирование (авторы оригинального текста напиминают еще раз, ООП) - точно так же, как все является объектом в ООП, в модели актора **_Все является актором_** (рискованное обобщение).

> Repeat this train of thought to yourself: everything is an actor. Everything is an actor. Everything is an actor! Think of designing your system like a hierarchy of people, with tasks being split up and delegated until they become small enough to be handled concisely by one actor.

Повторите себе эту цепочку мыслей: все есть актор. Все есть актор. Все есть актор! Размышляйте о своей системе как о иерархии людей, с задачами разбитыми и делегированными до такой степени, что с ними сможет справиться один актор.

> For now, we suggest you think of it like this: in OOP, you try to give every object a single, well-defined purpose, right? Well, the actor model is no different, except now the objects that you give a clear purpose to just happen to be actors.

Теперь мы предлагаем вам подумать об этом так: в ООП, вы пытаетесь дать каждому объекту одну, четко определенную цель, не так ли? Модель актора ничем не отличается, за исключением того, что теперь объекты, которым вы задаете четкую цель, являются акторами. 

Вообще в этом месте, стоит обратить внимание, что трактовка "актор = объект", является некоторым натягиванием совы на глобус. Т.к. например в функциональном мире, актор, хоть и остается в целом объектом, как правило воспринимается лишь функцией. Что будет видно, в пример ниже. (Я вряд ли буду сохранять пример оригинального кода.)

> **Further reading: [What is an Akka.NET Actor](http://petabridge.com/blog/akkadotnet-what-is-an-actor/)?**

**Дальнейшее чтение: [What is an Akka.NET Actor (Что есть Akka.NET Actor)](http://petabridge.com/blog/akkadotnet-what-is-an-actor/)?**

### How do actors communicate? | Как акторы взаимодейтсвуют друг с другом

> Actors communicate with each other just as humans do, by exchanging messages. These messages are just plain old C# classes.

Акторы общаются друг с другом как люди, обмениваясь сообщениями. Эти сообщения - просто ~~старые классы C#~~ обычные типы (н-р рекорды, DU и прочие достаточно простые и **неизменяемые объекты**).

```csharp
//this is a message!
public class SomeMessage{
	public int SomeValue {get; set}
}
```

```fsharp
// Это сообщение!
type SomeMessage = { SomeValue : int }
```

> We go into messages in detail in the next lesson, so don't worry about it for now. All you need to know is that you send messages by `Tell()`ing them to another actor.

Мы подробно рассмотрим сообщения в следующем уроке, не беспокойтесь об этом сейчас. Все что вам необходимо знать, это то, что вы отправляете сообщения посредством вызова метода `Tell()` на акторе получателе.

```csharp
//send a string to an actor
someActorRef.Tell("this is a message too!");
```

```fsharp
// отправить строку актору
someActorRef.Tell "this is a message too!"
```

Правда в F# распространенн оператор `<!`:

```fsharp
someActorRef "this is a message too!"
```

Вопреки ожиданиям оператора `!>` не предусмотрено. Почему не знаю.

### What can an actor do? | Что может делать актор?

> Anything you can code. Really :)

Все что вы можете закодить. Реально :)

> You code actors to handle messages they receive, and actors can do whatever you need them to in order to handle a message. Talk to a database, write to a file, change an internal variable, or anything else you might need.

Вы описываете акторам, то как они должны обрабатывать полученные сообщения, и акторы могут делать все, что вам нужно, чтобы обработвать сообщение. "Поговорите" с базой данных, напишите в файл, измените внутреннюю переменную или сделайте что-нибудь еще, что вам может понадобиться.

> In addition to processing a message it receives, an actor can:

Помимо обработки полученного сообщения, актор может:

> 1. Create other actors
> 1. Send messages to other actors (such as the `Sender` of the current message)
> 1. Change its own behavior and process the next message it receives differently

1. Создавать другие акторы
1. Отправлять сообщения другим акторам (например, `Sender` (отправителю) текущего сообщения)
1. Изменять свое собственное поведение, тем самым изменив порядок обработки следующего сообщения

> Actors are inherently asynchronous (more on this in a future lesson), and there is nothing about the [Actor Model](https://en.wikipedia.org/wiki/Actor_model) that says which of the above an actor must do, or the order it has to do them in. It's up to you.

Акторы по своей сути асинхронны (подробнее об этом в последюущих уроках), не существует никаких указаний от [Акторной Модели](https://ru.wikipedia.org/wiki/Модель_акторов)([EN](https://en.wikipedia.org/wiki/Actor_model)) на то, что актор и в каком порядке должен делать. Все это зависит только от вас.

### What kinds of actors are there? | Какие типы акторов существуют?

> All types of actors inherit from `UntypedActor`, but don't worry about that now. We'll cover different actor types later.

Все типы акторов наследуются от `UntypeActor`, но не стоит беспокоиться об эом сейчас. Мы рассмотрим различные типы акторов позднее. // А в случае Akkling мы вообще можем не вспоминать о существовании `UntypeActor` и прочего зоопарка.

> In Unit 1 all of your actors will inherit from [`UntypedActor`](http://getakka.net/docs/Working%20with%20actors#untypedactor-api "Akka.NET - UntypedActor API").

В Unit 1 все ваши акторы будут насловаться от [`UntypedActor`](http://getakka.net/docs/Working%20with%20actors#untypedactor-api "Akka.NET - UntypedActor API"). // Надюсь обойдемся без него.

### How do you make an actor? | Как создать актор?

> There are 2 key things to know about creating an actor:

Существуют две вещи, которые надо знать при создани актора:

> 1. All actors are created within a certain context. That is, they are "actor of" a context.
> 1. Actors need `Props` to be created. A `Props` object is just an object that encapsulates the formula for making a given kind of actor.

1. Все акторы создаются в определенном контексте. Т.е. они являются акторами некого контекста, по английски `actor of a context`.
1. Акторам для создания необходимы `Props`. `Props` - это объект, который просто инкапсулирует формулу создания данного типа акторов.

> We'll be going into `Props` in depth in lesson 3, so for now don't worry about it much. We've provided the `Props` for you in the code, so you just have to figure out how to use `Props` to make an actor.

Мы будем детально рассмотрим `Props` в уроке 3, так что пока не беспокойтесь об этом. Мы написали `Props` за вас, поэтому вам нужно просто выяснить, как использовать `Props`, чтобы сделать актора.

> The hint we'll give you is that your first actors will be created within the context of your actor system itself. See the exercise instructions for more.

Дадим подсказку, ваши первые акторы будут созданы в рамках акторной системы. Подробности смотрите в инструкциях к упражениням.

_В Akkling напрямую с обычным `Props` работать не придется, т.к. для C# это скорее вынужденная мера._

### What is an `ActorSystem`? | Что есть `ActorSystem`?

> An `ActorSystem` is a reference to the underlying system and Akka.NET framework. All actors live within the context of this actor system. You'll need to create your first actors from the context of this `ActorSystem`.

`ActorSystem` является ссылкой на подкапотную систему в платформе Akka.NET. Все акторы живущие в рамках контекста принадлежат системе. Вам потребуется создать свой первый актор использую `ActorSystem` в качестве контекста.

> By the way, the `ActorSystem` is a heavy object: create only one per application.

Кстати, `ActorSystem` - это крайне тяжелый объект: создавайте лишь одну систему на приложение.

> Aaaaaaand... go! That's enough conceptual stuff for now, so dive right in and make your first actors.

\*Звукоподражание либо зевоте, либо крику отчаянья\*. Достаточно концепций, чтобы погрузиться и сделать свои первые акторы.

## Exercise | Упражнение

> Let's dive in!

_Погрузимся в это. // Не знаю, как это перевести нормально._

>> Note: Within the sample code there are sections clearly marked `"YOU NEED TO FILL IN HERE"` - find those regions of code and begin filling them in with the appropriate functionality in order to complete your goals.

> Заметьте: В предоставленном примере кода есть разделы, которые четко обозначены `"YOU NEED TO FILL IN HERE"` (ВАМ НУЖНО ЗАПОЛНЯТЬ ЗДЕСЬ) - найдите их и наполните их соответствующей вашим целям функциональностью.

### Launch the fill-in-the-blank sample | Запустите пример

> Go to the [DoThis](../DoThis/) folder and open [WinTail](../DoThis/WinTail.sln) in Visual Studio. The solution consists of a simple console application and only one Visual Studio project file.

Перейдите к папке [Akkling](../Akkling/) и откройте [WinTail](../Akkling/WinTail.sln) в Visual Studio. Решение содержит простое консольное приложение только с одним файлом проекта.

> You will use this solution file through all of Unit 1.

Вы будете использовать данное решение на протяжени всего Unit 1.

### Install the latest Akka.NET NuGet package | Установите последнюю версию Akkling через Paket

> In the Package Manager Console, type the following command:
> 
> ```
> Install-Package Akka
> ```
> 
> This will install the latest Akka.NET binaries, which you will need in order to compile this sample.
>
> Then you'll need to add the `using` namespace to the top of `Program.cs`:
> 
> ```csharp
> // in Program.cs
> using Akka.Actor;
> ```

Опустим объяснения для NuGet. В `paket.dependecies` и `paket.references` уже прописаны необходимые пакеты. Вам достаточно вызывать `.paket\paket.exe install` **из корня репозитория**.

Т.к. объем кода позволяет не выходить за пределы файлы, вам не потребуется подключать какие либо `Akka.Actor`. Но на будущее можете запомнить, что большая часть необходимых для Akka.NET типов находится в `Akka.Actor`. Но в случае Akkling, скорее всего вам хватит одного `open Akkling`.

### Где я?

Текущее приложение содержит в себе 4 блока.

#### `Console`

Несколько расширений для Console. Конкретно для печати с использованием каких либо цветов. Данной утилиты не было в оригинале, но я счел слишком дорогим мероприятием писать по три строки каждый раз вручную.

#### `ConsoleWriterActor`

Модуль содержит в себе single DU тип `Message`, который призван:

1. Защищать от сообщение не адресованных данному актору.
2. Быть маркером, которым можно типизировать параметр `writer` в `ConsoleReaderActor`. // Ниже.

Кроме `Message` содержит еще функцию `create ()`. По последней строке можно догадаться, что `create` как раз и создает выше описанный `Props`.

#### `ConsoleReaderActor`

Модуль содержит уже виденный набор в виде типа `Message` и метода `create`. Отличием является наличие не unit параметра у `create`. Как видно из аннотации, данный метод получает `IActorRef<ConsoleWriterActor.Message>`. Объект подобного типа можно получить заспавнив актор от `Props` полученного из `ConsoleWriterActor.create`.

Также в модуле присутсвует `exitCommand`, которая будет использоваться в качестве ключа.

#### Program

В остальной части Program даны:

* Функция вывода подсказки
* Привязка акторной системы (ее инициализация лежит на нас)
* Основной метод Main

### Make your first `ActorSystem` | Создайте свою первую `ActorSystem`

> Go to `Program.cs` and add this to create your first actor system:
> 
> ```csharp
> MyActorSystem = ActorSystem.Create("MyActorSystem");
> ```
> >
> > **NOTE:** When creating `Props`, `ActorSystem`, or `ActorRef` you will very rarely see the `new` keyword. These objects must be created through the factory methods built into Akka.NET. If you're using `new` you might be making a mistake.

В `Program.fs` (пока что наш единственный рабочий файл в рамках урока) есть декларация `myActorSystem`. Создайте свою первую систему (прямо по месту декларации):

```fsharp
let myActorSystem =
    Configuration.defaultConfig()
    |> System.create "MyActorSystem"
```

В отличии от C#, система потребует дополнительной конфигурации (`Configuration.defaultConfig()`). Что это такое, будет расказанно в Unit 2. Пока же достаточно запомнить эти строки, как способ инициализации системы по умолчанию.

### Make ConsoleReaderActor & ConsoleWriterActor | Создайте ConsoleReaderActor и ConsoleWriterActor 

> The actor classes themselves are already defined, but you will have to make your first actors.
> 
> Again, in `Program.cs`, add this just below where you made your `ActorSystem`:
> 
> ```csharp
> var consoleWriterActor = MyActorSystem.ActorOf(Props.Create(() =>
> new ConsoleWriterActor()));
> var consoleReaderActor = MyActorSystem.ActorOf(Props.Create(() =>
> new ConsoleReaderActor(consoleWriterActor)));
> ```
> 
> We will get into the details of `Props` and `ActorRef`s in lesson 3, so don't worry about them much for now. Just know that this is how you make an actor.

`Props` акторов уже определены, но вам необходимо создать свои первые акторы.

В Akkling самым простым способом создания акторов является функция `spawnAnonumous`. Она принимает `IActorRefFactory` (система или актор), и `Props<'message>`. Добавьте следующие строки в соответсвующем месте:

```fsharp
let writer =
    ConsoleWriterActor.create ()
    |> spawnAnonymous myActorSystem
let reader =
    ConsoleReaderActor.create writer
    |> spawnAnonymous myActorSystem
```

Как говорилось ранее, контекстом первых акторов стала сама система.

### Have ConsoleReaderActor Send a Message to ConsoleWriterActor | ...

> Time to put your first actors to work!
> 
> You will need to do the following:
> 
> 1. ConsoleReaderActor is set up to read from the console. Have it send a message to ConsoleWriterActor containing the content that it just read.
> 
> 	```csharp
> 	// in ConsoleReaderActor.cs
> 	_consoleWriterActor.Tell(read);
> 	```
> 
> 2. Have ConsoleReaderActor send a message to itself after sending a message to ConsoleWriterActor. This is what keeps the read loop going.
> 
> 	```csharp
> 	// in ConsoleReaderActor.cs
> 	Self.Tell("continue");
> 	```
> 3. Send an initial message to ConsoleReaderActor in order to get it to start reading from the console.
> 
> 	```csharp
> 	// in Program.cs
> 	consoleReaderActor.Tell("start");
> 	```

Время заставить акторы работать!

Вам потребуется сделать следующее:

1. `ConsoleReaderActor` настроен на чтение из консоли. Пусть он отправит сообщение `ConsoleWriterActor`-у, содержащее текст, который он только что прочитал.
    
	АХТУНГ!!! В проект закралась ошибка, добавьте `not` в проверку на пустоту:  `if not (String.IsNullOrEmpty read)`.

	```fsharp
	writer <! ConsoleWriterActor.Message message
	```

2. Далее необходимо, чтобы `ConsoleReaderActor` отправил сообщение самому себе после того, как отправит сообщение `ConsoleWriterActor`-у. Подобное поведение позволит продолжить цикл. Выглядит это приблизительно так:
    
	```fsharp
	context.Self <! Message "continue
	```

	Однако можно заметить, что `context` не существует в текущей области видимости. Это логично, т.к. изначально мы использовали упрощенную запись. 

	Перепишите функцию следующим образом:

	```fsharp
	actorOf2 <| fun context ->
		let rec behaviour (Message message) =
			let read = Console.ReadLine()
			if not (String.IsNullOrEmpty read)
				&& String.Equals(read, exitCommand, StringComparison.OrdinalIgnoreCase)
			then
				// shut down the system (acquire handle to system via
				// this actors context)
				Stop :> Effect<_>
			else
				writer <! ConsoleWriterActor.Message message
				context.Self <! Message "continue"
				become behaviour
		behaviour
	|> props
	```

3. Осталось отправить инициирующее сообщение в `reader` (в `Program.main`), тем самым начав чтение из консоли.

	```fsharp
	reader <! ConsoleReaderActor.Message "start"
	```

### Step 5: Build and Run! | Шаг 5: Скомпилируйте и запустите

> Once you've made your edits, press `F5` to compile and run the sample in Visual Studio.

После внесения всех изменений нажмите `F5` чтобы скомпилировать и запустить пример в  Visual Studio.

> You should see something like this, when it is working correctly:

Вы должны увидеть нечто подобное:

![Petabridge Akka.NET Bootcamp Lesson 1.1 Correct Output](Images/example.png)

>> **N.B.** In Akka.NET 1.0.8 and later, you'll receive a warning about the JSON.NET serializer being deprecated in a future released of Akka.NET (1.5). This is true and you can [learn how to start using the beta of the Wire serializer package here](http://getakka.net/docs/Serialization#how-to-setup-wire-as-default-serializer). This is mainly meant to be a warning for Akka.NET users running Akka.Persistence or Akka.Remote, which both depend on the default serializer.

> **N.B.** В Akka.NET 1.0.8. или позднее вы получите предупреждение о том, что JSON.NET сериализатор будет удален из будущих версий Akka.NET (1.5). Но мне лень переводить, т.к. по умолчанию в Akkling установлен другой тип сериализации, и нас сие не касается.


### Once you're done | Когда закончите

> Compare your code to the code in the [Completed](Completed/) folder to see what the instructors included in their samples.

Сравните ваш код с тем, что представлен в папке [Completed](Completed/), чтобы увидеть, что преподаватели включили в свои примеры.

## Great job! Onto Lesson 2! | Хорошая работа! Переходим к уроку 2!

> Awesome work! Well done on completing your first lesson.

Потрясающая работа! Вы молодец.

> **Let's move onto [Lesson 2 - Defining and Handling Different Types of Messages](../lesson2/README.md).**

**Перейдем к [Lesson 2 - Определение и обработка различных типов сообщений](../lesson2/README.md).**

## Any questions? | Вопросы?

> Come ask any questions you have, big or small, [in this ongoing Bootcamp chat with the Petabridge & Akka.NET teams](https://gitter.im/petabridge/akka-bootcamp).

Вы можете задавать любые вопросы [в чате Bootcamp, где на них ответят команды Petabridge и Akka.NET](https://gitter.im/petabridge/akka-bootcamp). Хотя скорее всего вам будет лучше спросить конкретно меня (я же не выложил это в публичный доступ?).

### Problems with the code? | Проблемы с кодом?

> If there is a problem with the code running, or something else that needs to be fixed in this lesson, please [create an issue](https://github.com/petabridge/akka-bootcamp/issues) and we'll get right on it. This will benefit everyone going through Bootcamp.

Если есть проблема с запуском кода или что-то еще, что необходимо исправить в этом уроке, пожалуйста, создайте issue или тупо напишите мне. Это принесет пользу всем кто проходит через Bootcamp.