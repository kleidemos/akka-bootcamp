# Akka.NET Bootcamp - Unit 1: Beginning Akka.NET | Akkling Bootcamp - Unit 1: Beginning akkling

> In Unit 1, we will learn the fundamentals of how the actor model and Akka.NET work.

В Unit 1, мы изучим фундаментальные принципы работы акторной модели и Akka.NET в частности. **Текcт будет в вольной форме адаптирован под Akkling.** (Если мне будет не лень) Без понятия, есть ли перевод на русский (попытки видел), но решил попробовать.

## Concepts you'll learn | Что вы изучите

> *NIX systems have the `tail` command built-in to monitor changes to a file (such as tailing log files), whereas Windows does not. We will recreate `tail` for Windows, and use the process to learn the fundamentals.

У *NIX систем есть встроенная командная утилита `tail`, которая отслеживает изменения в файле (например журналах), в то время как в Windows ничего подобного нет. Мы воссоздадим `tail` для Windows, заодно изучив основы Akka.

> In Unit 1 you will learn the following:

В Unit 1 вы узначете следующие вещи:

> 1. How to create your own `ActorSystem` and actors;
> 2. How to send messages actors and how to handle different types of messages;
> 3. How to use `Props` and `IActorRef`s to build loosely coupled systems.
> 4. How to use actor paths, addresses, and `ActorSelection` to send messages to actors.
> 5. How to create child actors and actor hierarchies, and how to supervise children with `SupervisionStrategy`.
> 6. How to use the Actor lifecycle to control actor startup, shutdown, and restart behavior.

1. Как создавать свою собственную систему и акторы;
1. Как отправлять сообщения акторам и как обрабатывать различные типы сообщений;
1. Как использовать `Props` и `IActorRef` для построения слабосвязанных систем;
1. Как использовать пути акторов, адреса и `ActorSelection` для отправки сообщения акторам;
1. Как создавать акторов-потомков и иерархии акторов, и как наблюдать за потомками с помощью `SupervisionStrategy`;
1. Как использовать жизненный цикл акторов для управления запуском, завершением работы и перезапуском субъекта.

## Using Xamarin? | Используете Xamarin?
> Since Unit 1 relies heavily on the console, you'll need to make a small tweaks before beginning. You need to set up your `WinTail` project file (not the solution) to use an **external console**.

Поскольку Unit 1 в значительной степени зависит от консоли, вам потребуется проделать некоторую настройку перед началом. Вам надо указать вашему `WinTail` проекту (не решению) использовать **external console**.

> To set this up:

Сделайте следующее: // Лучше пока не переводить.

1. Click on the `WinTail` project (not the solution)
2. Navigate to `Project > WinTail Options` in the menu
3. Inside `WinTail Options`, navigate to `Run > General`
4. Select `Run on external console`
5. Click `OK`

Here is a demonstration of how to set it up:
![Configure Xamarin to use external console](../../images/xamarin.gif)

## Table of Contents | Содержание

> 1. **[Lesson 1 - Actors and the `ActorSystem`](lesson1/README.md)**
> 2. **[Lesson 2 - Defining and Handling Messages](lesson2/README.md)**
> 3. **[Lesson 3: Using `Props` and `IActorRef`s](lesson3/README.md)**
> 4. **[Lesson 4: Child Actors, Hierarchies, and Supervision](lesson4/README.md)**
> 5. **[Lesson 5: Looking up actors by address with `ActorSelection`](lesson5/README.md)**
> 6. **[Lesson 6: The Actor Lifecycle](lesson6/README.md)**

1. **[Lesson 1: Акторы и `ActorSystem`](lesson1/README.md)**
2. **[Lesson 2: Определение и обработка сообщений](lesson2/README.md)**
3. **[Lesson 3: Использование `Props` и `IActorRef`s](lesson3/README.md)**
4. **[Lesson 4: Потомки, иерархии и сопровождение акторов](lesson4/README.md)**
5. **[Lesson 5: Поиск акторов по адресу с помощью `ActorSelection`](lesson5/README.md)**
6. **[Lesson 6: Жизненый цикл акторов](lesson6/README.md)**

## Get Started | _Приступим_

> To get started, [go to the /DoThis/ folder](DoThis/) and open `WinTail.sln`.

Чтобы начать, перейдите к [папке /Akkling/](Akkling/) и откройте `WinTail.sln`.

> And then go to [Lesson 1](lesson1/README.md).

И затем перейдите к [Lesson 1](lesson1/README.md).