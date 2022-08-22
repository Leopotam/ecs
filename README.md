# LeoECS - Легковесный C# Entity Component System фреймворк
Производительность, нулевые или минимальные аллокации, минимизация использования памяти, отсутствие зависимостей от любого игрового движка - это основные цели данного фреймворка.

> **ВАЖНО!** РАЗРАБОТКА ПРЕКРАЩЕНА, ВОЗМОЖНО ТОЛЬКО ИСПРАВЛЕНИЕ ОБНАРУЖЕННЫХ ОШИБОК. СОСТОЯНИЕ СТАБИЛЬНОЕ, ИЗВЕСТНЫХ ОШИБОК НЕ ОБНАРУЖЕНО. ЗА НОВЫМ ФУНКЦИОНАЛОМ СТОИТ СЛЕДИТЬ В РЕПОЗИТОРИИ [EcsLite](https://github.com/Leopotam/ecslite).

> **ВАЖНО!** Не забывайте использовать `DEBUG`-версии билдов для разработки и `RELEASE`-версии билдов для релизов: все внутренние проверки/исключения будут работать только в `DEBUG`-версиях и удалены для увеличения производительности в `RELEASE`-версиях.

> **ВАЖНО!** LeoEcs-фрейморк **не потокобезопасен** и никогда не будет таким! Если вам нужна многопоточность - вы должны реализовать ее самостоятельно и интегрировать синхронизацию в виде ecs-системы.


# Содержание
* [Социальные ресурсы](#Социальные-ресурсы)
* [Установка](#Установка)
    * [В виде unity модуля](#В-виде-unity-модуля)
    * [В виде исходников](#В-виде-исходников)
* [Основные типы](#Основные-типы)
    * [Компонент](#Компонент)
    * [Сущность](#Сущность)
    * [Система](#Система)
* [Инъекция данных](#Инъекция-данных)
* [Специальные типы](#Специальные-типы)
    * [EcsFilter<T>](#EcsFilterT)
    * [EcsWorld](#EcsWorld)
    * [EcsSystems](#EcsSystems)
* [Интеграция с движками](#Интеграция-с-движками)
    * [Unity](#Unity)
    * [Кастомный движок](#Кастомный-движок)
* [Проекты, использующие LeoECS](#Проекты-использующие-LeoECS)
    * [С исходниками](#С-исходниками)
    * [Выпущенные игры](#Выпущенные-игры)
* [Расширения](#Расширения)
* [Лицензия](#Лицензия)
* [ЧаВо](#ЧаВо)

# Социальные ресурсы
[![discord](https://img.shields.io/discord/404358247621853185.svg?label=enter%20to%20discord%20server&style=for-the-badge&logo=discord)](https://discord.gg/5GZVde6)

# Установка

## В виде unity модуля
Поддерживается установка в виде unity-модуля через git-ссылку в PackageManager или прямое редактирование `Packages/manifest.json`:
```
"com.leopotam.ecs": "https://github.com/Leopotam/ecs.git",
```
По умолчанию используется последняя релизная версия. Если требуется версия "в разработке" с актуальными изменениями - следует переключиться на ветку `develop`:
```
"com.leopotam.ecs": "https://github.com/Leopotam/ecs.git#develop",
```

## В виде исходников
Код так же может быть склонирован или получен в виде архива со страницы релизов.

# Основные типы

## Компонент
Является контейнером для данных пользователя и не должен содержать логику (допускаются минимальные хелперы, но не куски основной логики):
```c#
struct WeaponComponent {
    public int Ammo;
    public string GunName;
}
```

## Сущность
Сама по себе ничего не значит и не существует, является исключительно контейнером для компонентов. Реализована как `EcsEntity`:
```c#
// NewEntity() используется для создания новых сущностей в контексте мира.
EcsEntity entity = _world.NewEntity ();

// Get() возвращает существующий на сущности компонент. Если компонент не существовал - он будет добавлен автоматически.
// Следует обратить внимание на "ref" - компоненты должны обрабатываться по ссылке.
ref Component1 c1 = ref entity.Get<Component1> ();
ref Component2 c2 = ref entity.Get<Component2> ();

// Del() удаляет компонент с сущности. Если это был последний компонент - сущность будет удалена автоматически. Если компонент не существовал - ошибки не будет.
entity.Del<Component2> ();

// Replace() выполняет замену компонента новым экземпляром. Если старый компонент не существовал - новый будет добавлен без ошибки.
WeaponComponent weapon = new WeaponComponent () { Ammo = 10, GunName = "Handgun" };
entity.Replace (weapon);

// Replace() позволяет выполнять "чейнинг" создания компонентов:
EcsEntity entity2 = world.NewEntity ();
entity2.Replace (new Component1 { Id = 10 }).Replace (new Component2 { Name = "Username" });

// Любая сущность может быть скопирована вместе с компонентами:
EcsEntity entity2Copy = entity2.Copy ();

// Любая сущность может "передать" свои компоненты другой сущности (сама будет уничтожена):
var newEntity = world.NewEntity ();
entity2Copy.MoveTo (newEntity); // все компоненты с "entity2Copy" переместятся на "newEntity", а "entity2Copy" будет удалена.

// Любая сущность может быть удалена, при этом сначала все компоненты будут автоматически удалены и только потом энтити будет считаться уничтоженной. 
entity.Destroy ();
```

> **ВАЖНО!** Сущности не могут существовать без компонентов и будут автоматически уничтожаться при удалении последнего компонента на них.

## Система
Является контейнером для основной логики для обработки отфильтрованных сущностей. Существует в виде пользовательского класса, реализующего как минимум один из `IEcsInitSystem`, `IEcsDestroySystem`, `IEcsRunSystem` (и прочих поддерживаемых) интерфейсов:
```c#
class UserSystem : IEcsPreInitSystem, IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem, IEcsPostDestroySystem {
    public void PreInit () {
        // Будет вызван один раз в момент работы EcsSystems.Init() и до срабатывания IEcsInitSystem.Init().
    }

    public void Init () {
        // Будет вызван один раз в момент работы EcsSystems.Init() и после срабатывания IEcsPreInitSystem.PreInit().
    }
    
    public void Run () {
        // Будет вызван один раз в момент работы EcsSystems.Run().
    }

    public void Destroy () {
        // Будет вызван один раз в момент работы EcsSystems.Destroy() и до срабатывания IEcsPostDestroySystem.PostDestroy().
    }

    public void PostDestroy () {
        // Будет вызван один раз в момент работы EcsSystems.Destroy() и после срабатывания IEcsDestroySystem.Destroy().
    }
}
```

# Инъекция данных
Все поля **ecs-систем**, совместимые c `EcsWorld` и `EcsFilter<T>` будут автоматически инициализированы валидными экземплярами соответствующих типов:
```c#
class HealthSystem : IEcsSystem {
    // Поля с авто-инъекцией.
    EcsWorld _world;
    EcsFilter<WeaponComponent> _weaponFilter;
}
```

Экземпляр любого кастомного типа (класса) может быть инъецирован с помощью метода `EcsSystems.Inject()`:
```c#
class SharedData {
    public string PrefabsPath;
}
...
SharedData sharedData = new SharedData { PrefabsPath = "Items/{0}" };
EcsSystems systems = new EcsSystems (world);
systems
    .Add (new TestSystem1 ())
    .Inject (sharedData)
    .Init ();
```

Каждая система будет просканирована на наличие полей, совместимых по типу с последующей инъекцией:
```c#
class TestSystem1 : IEcsInitSystem {
    // Поле с авто-инъекцией.
    SharedData _sharedData;
    
    public void Init() {
        var prefabPath = string.Format (_sharedData.Prefabspath, 123);
        // prefabPath = "Items/123" к этому моменту.
    } 
}
```
> **ВАЖНО!** Для инъекции подходят только нестатичные public/private-поля конечного класса системы, либо public/protected-поля базовых классов. Все остальные поля будут проигнорированы!

# Специальные типы

## EcsFilter<T>
Является контейнером для хранения отфильтрованных сущностей по наличию или отсутствию определенных компонентов:
```c#
class WeaponSystem : IEcsInitSystem, IEcsRunSystem {
    // Поля с авто-инъекцией.
    EcsWorld _world;
    // Мы хотим получить все сущности с компонентом "WeaponComponent"
    // и без компонента "HealthComponent".
    EcsFilter<WeaponComponent>.Exclude<HealthComponent> _filter;

    public void Init () {
        _world.NewEntity ().Get<WeaponComponent> ();
    }

    public void Run () {
        foreach (int i in _filter) {
            // Сущность, которая точно содержит компонент "WeaponComponent".
            ref EcsEntity entity = ref _filter.GetEntity (i);

            // Get1() позволяет получить доступ по ссылке на компонент,
            // указанный первым в списке ограничений фильтра ("WeaponComponent").
            ref WeaponComponent weapon = ref _filter.Get1 (i);
            weapon.Ammo = System.Math.Max (0, weapon.Ammo - 1);
        }
    }
}
```

Любые компоненты из `Include`-списка ограничений фильтра могут быть получены через вызовы `EcsFilter.Get1()`, `EcsFilter.Get2()` и т.д - нумерация идет в том же порядке, что и в списке ограничений.

Если в компоненте нет данных и он используется исключительно как флаг-признак для фильтрации, то компонент может реализовать интерфейс `IEcsIgnoreInFilter` - это поможет уменьшить потребление памяти фильтром и немного увеличить производительность:
```c#
struct Component1 { }

struct Component2 : IEcsIgnoreInFilter { }

class TestSystem : IEcsRunSystem {
    EcsFilter<Component1, Component2> _filter;

    public void Run () {
        foreach (var i in _filter) {
            // Мы можем получить компонент "Component1".
            ref var component1 = ref _filter.Get1 (i);

            // Мы не можем получить "Component2" - кеш внутри фильтра не существует и будет выкинуто исключение.
            ref var component2 = ref _filter.Get2 (i);
        }
    }
}
```

> **ВАЖНО!**: Фильтры поддерживают до 6 `Include`-ограничений и 2 `Exclude`-ограничений. Чем меньше ограничений в фильтре - тем он быстрее работает.

> **ВАЖНО!** Нельзя использовать несколько фильтров с одинаковым списком ограничений, но выставленных в разном порядке - в `DEBUG`-версии будет выкинуто исключение с описанием конфликтующих фильтров.

> **ВАЖНО!** Один и тот же компонент не может быть в списках "Include" и "Exclude" одного фильтра одновременно.

## EcsWorld
Является контейнером для всех сущностей и фильтров, данные каждого экземпляра уникальны и изолированы от других миров.

> **ВАЖНО!** Необходимо вызывать `EcsWorld.Destroy()` у экземпляра мира если он больше не нужен.

## EcsSystems
Является контейнером для систем, которыми будет обрабатываться `EcsWorld`-экземпляр мира:
```c#
class Startup : MonoBehaviour {
    EcsWorld _world;
    EcsSystems _systems;

    void Start () {
        // Создаем окружение, подключаем системы.
        _world = new EcsWorld ();
        _systems = new EcsSystems (_world)
            .Add (new WeaponSystem ());
        _systems.Init ();
    }
    
    void Update () {
        // Выполняем все подключенные системы.
        _systems.Run ();
    }

    void OnDestroy () {
        // Уничтожаем подключенные системы.
        _systems.Destroy ();
        // Очищаем окружение.
        _world.Destroy ();
    }
}
```

Экземпляр `EcsSystems` может быть использован как обычная ecs-система (вложена в другую `EcsSystems`):
```c#
// Инициализация.
EcsSystems nestedSystems = new EcsSystems (_world).Add (new NestedSystem ());

// Нельзя вызывать nestedSystems.Init() здесь,
// "rootSystems" выполнит этот вызов автоматически.
EcsSystems rootSystems = new EcsSystems (_world).Add (nestedSystems);
rootSystems.Init ();

// В цикле обновления нельзя вызывать nestedSystems.Run(),
// "rootSystems" выполнит этот вызов автоматически.
rootSystems.Run ();

// Очистка.
// Нельзя вызывать nestedSystems.Destroy() здесь,
// "rootSystems" выполнит этот вызов автоматически.
rootSystems.Destroy ();
```

Любая `IEcsRunSystem` система (включая вложенные `EcsSystems`) может быть включен или выключен из списка обработки:
```c#
class TestSystem : IEcsRunSystem {
    public void Run () { }
}
EcsSystems systems = new EcsSystems (_world);
systems.Add (new TestSystem (), "my special system");
systems.Init ();
var idx = systems.GetNamedRunSystem ("my special system");

// "state" будет иметь значение "true", все системы включены по умолчанию.
var state = systems.GetRunSystemState (idx);

// Выключаем систему по ее индексу.
systems.SetRunSystemState (idx, false);
```

# Интеграция с движками

## Unity
> Проверено на Unity 2020.3 (не зависит от нее) и содержит asmdef-описания для компиляции в виде отдельных сборок и уменьшения времени рекомпиляции основного проекта.

[Интеграция в Unity editor](https://github.com/Leopotam/ecs-unityintegration) содержит шаблоны кода, а так же предоставляет мониторинг состояния мира.


## Кастомный движок
> Для использования фреймворка требуется C#7.3 или выше.

Каждая часть примера ниже должна быть корректно интегрирована в правильное место выполнения кода движком:
```c#
using Leopotam.Ecs;

class EcsStartup {
    EcsWorld _world;
    EcsSystems _systems;

    // Инициализация окружения.
    void Init () {        
        _world = new EcsWorld ();
        _systems = new EcsSystems (_world);
        _systems
            // Системы с основной логикой должны
            // быть зарегистрированы здесь, порядок важен:
            // .Add (new TestSystem1 ())
            // .Add (new TestSystem2 ())
            
            // OneFrame-компоненты должны быть зарегистрированы
            // в общем списке систем, порядок важен:
            // .OneFrame<TestComponent1> ()
            // .OneFrame<TestComponent2> ()
            
            // Инъекция должна быть произведена здесь,
            // порядок не важен:
            // .Inject (new CameraService ())
            // .Inject (new NavMeshSupport ())
            .Init ();
    }

    // Метод должен быть вызван из
    // основного update-цикла движка.
    void UpdateLoop () {
        _systems?.Run ();
    }

    // Очистка.
    void Destroy () {
        if (_systems != null) {
            _systems.Destroy ();
            _systems = null;
            _world.Destroy ();
            _world = null;
        }
    }
}
```

# Проекты, использующие LeoECS
## С исходниками
* ["MatchTwo"](https://github.com/cadfoot/unity-ecs-match-two)

  [![](https://img.youtube.com/vi/Y3DwZmPCPSk/0.jpg)](https://www.youtube.com/watch?v=Y3DwZmPCPSk)


* ["Bubble shooter"](https://github.com/cadfoot/unity-ecs-bubble-shooter)

  [![](https://img.youtube.com/vi/l19wREGUf1k/0.jpg)](https://www.youtube.com/watch?v=l19wREGUf1k)


* ["Frantic Architect Remake"](https://github.com/cadfoot/unity-ecs-fran-arch)

  [![](https://img.youtube.com/vi/YAfHDyBl7Fg/0.jpg)](https://www.youtube.com/watch?v=YAfHDyBl7Fg)


* ["Mahjong Solitaire"](https://github.com/cadfoot/unity-ecs-mahjong-solitaire)

  [![](https://img.youtube.com/vi/FxOcqVwue9g/0.jpg)](https://www.youtube.com/watch?v=FxOcqVwue9g)


* ["3D Platformer"](https://github.com/supremestranger/3D-Platformer)
  [![](https://camo.githubusercontent.com/dcd2f525130d73f4688c1f1cfb12f6e37d166dae23a1c6fac70e5b7873c3ab21/68747470733a2f2f692e6962622e636f2f686d374c726d342f506c6174666f726d65722e706e67)](https://github.com/supremestranger/3D-Platformer)


* ["SpaceInvaders (Guns&Bullets variation)"](https://github.com/GoodCatGames/SpaceInvadersEcs)
  [![](https://github.com/GoodCatGames/SpaceInvadersEcs/raw/master/docs/SpaceInvadersImage.png)](https://github.com/GoodCatGames/SpaceInvadersEcs)


* ["Runner"](https://github.com/t1az2z/RunnerECS)


* ["Pacman"](https://github.com/SH42913/pacmanecs)

## Released games
* ["OUTERBLAST"](https://stuwustudio.itch.io/outerblast)
  
  [![](https://img.youtube.com/vi/PqCJsiyogTg/0.jpg)](https://www.youtube.com/watch?v=PqCJsiyogTg)


* ["Idle Delivery City Tycoon"](https://play.google.com/store/apps/details?id=com.Arctic.IdleTransportTycoon)
  
  [![](https://img.youtube.com/vi/FV-0Dq4kcy8/0.jpg)](https://www.youtube.com/watch?v=FV-0Dq4kcy8)


* ["Kangaeru!"](https://kangaeru.space/)

  [![](https://img.youtube.com/vi/FcAw6QzzDdA/0.jpg)](https://youtu.be/FcAw6QzzDdA)

* ["Boom Race"](https://play.google.com/store/apps/details?id=com.ZlodeyStudios.BoomRace)
* ["HypnoTap"](https://play.google.com/store/apps/details?id=com.ZlodeyStudios.HypnoTap)
* ["TowerRunner Revenge"](https://play.google.com/store/apps/details?id=ru.zlodey.towerrunner20)
* ["Natives"](https://alex-kpojb.itch.io/natives-ecs)

# Расширения
* [Интеграция в редактор Unity](https://github.com/Leopotam/ecs-unityintegration)
* [Поддержка Unity uGui](https://github.com/Leopotam/ecs-ui)
* [Поддержка многопоточности](https://github.com/Leopotam/ecs-threads)
* [SharpPhysics2D](https://github.com/7Bpencil/sharpPhysics/tree/LeoECS)
* [UniLeo - Unity scene data converter](https://github.com/voody2506/UniLeo)
* [Unity Physx events support](https://github.com/supremestranger/leoecs-physics)

# Лицензия
Фреймворк выпускается под двумя лицензиями, [подробности тут](./LICENSE.md).

В случаях лицензирования по условиям MIT-Red не стоит расчитывать на
персональные консультации или какие-либо гарантии.

# ЧаВо

### Я хочу знать - существовал ли компонент на сущности до вызова Get() для разной инициализации полученных данных. Как я могу это сделать?

Если не важно - существовал компонент ранее и просто нужна уверенность, что он теперь существует достаточно вызова `EcsEntity.Get<T>()`.

Если нужно понимание, что компонент существовал ранее - это можно проверить с помощью вызова `EcsEntity.Has<T>()`.  

### Я хочу одну систему вызвать в `MonoBehaviour.Update()`, а другую - в `MonoBehaviour.FixedUpdate()`. Как я могу это сделать?

Для разделения систем на основе разных методов из `MonoBehaviour` необходимо создать под каждый метод отдельную `EcsSystems`-группу:
```c#
EcsSystems _update;
EcsSystems _fixedUpdate;

void Start () {
    var world = new EcsWorld ();
    _update = new EcsSystems (world).Add (new UpdateSystem ());
    _update.Init ();
    _fixedUpdate = new EcsSystems (world).Add (new FixedUpdateSystem ());
    _fixedUpdate.Init ();
}

void Update () {
    _update.Run ();
}

void FixedUpdate () {
    _fixedUpdate.Run ();
}
```

### Мне нравится как работает автоматическая инъекция данных, но хотелось бы часть полей исключить. Как я могу сделать это?

Для этого достаточно пометить поле системы атрибутом `[EcsIgnoreInject]`:
```c#
// Это поле будет обработано инъекцией.
EcsFilter<C1> _filter1;

// Это поле будет проигнорировано инъекцией.
[EcsIgnoreInject] EcsFilter<C2> _filter2;
```

### Меня не устраивают значения по умолчанию для полей компонентов. Как я могу это настроить?

Компоненты поддерживают кастомную настройку значений через реализацию интерфейса `IEcsAutoReset<>`:
```c#
struct MyComponent : IEcsAutoReset<MyComponent> {
    public int Id;
    public object LinkToAnotherComponent;

    public void AutoReset (ref MyComponent c) {
        c.Id = 2;
        c.LinkToAnotherComponent = null;
    }
}
```
Этот метод будет автоматически вызываться для всех новых компонентов, а так же для всех только что удаленных, до помещения их в пул.
> **ВАЖНО!** В случае применения `IEcsAutoReset` все дополнительные очистки/проверки полей компонента отключаются, что может привести к утечкам памяти. Ответственность лежит на пользователе!

> **ВАЖНО!**: Компоненты, реализующие `IEcsAutoReset` не совместимы с вызовами `entity.Replace()`. Рекомендуется не использовать `entity.Replace()` или любые другие способы полной перезаписи компонентов.

### Я использую компоненты как "события", которые живут 1 цикл, а потом удаляются в конце отдельной системой. Получается много лишнего кода, есть ли более простой способ?

Для автоматической очистки компонентов, которые должны жить один цикл, место их очистки может быть зарегистрировано в общем списке систем внутри `EcsSystems`:
```c#
struct MyOneFrameComponent { }

EcsSystems _update;

void Start () {
    var world = new EcsWorld ();
    _update = new EcsSystems (world);
    _update
        .Add (new CalculateSystem ())
        // Все "MyOneFrameComponent" компоненты будут
        // удалены здесь.
        .OneFrame<MyOneFrameComponent> ()
        // Здесь можно быть уверенным, что ни один
        // "MyOneFrameComponent" не существует.
        .Add (new UpdateSystem ())
        .Init ();
}

void Update () {
    _update.Run ();
}
```

### Мне нужен больший контроль над размерами кешей, которые мир использует в момент создания. Как я могу сделать это?

Мир может быть создан с явным указанием `EcsWorldConfig`-конфигурации:
```c#
var config = new EcsWorldConfig () {
    // Размер по умолчанию для World.Entities.
    WorldEntitiesCacheSize = 1024,
    // Размер по умолчанию для World.Filters.
    WorldFiltersCacheSize = 128,
    // Размер по умолчанию для World.ComponentPools.
    WorldComponentPoolsCacheSize = 512,
    // Размер по умолчанию для Entity.Components (до удвоения).
    EntityComponentsCacheSize = 8,
    // Размер по умолчанию для Filter.Entities.
    FilterEntitiesCacheSize = 256,
};
var world = new EcsWorld(config);
```

### Мне нужно больше чем 6-"Include" и 2-"Exclude" ограничений для компонентов в фильтре. Как я могу сделать это?

> **ВАЖНО!** Это приведет к модификации исходного кода фреймворка и несовместимости с обновлениями.
> 
Необходимо воспользоваться [кодогенерацией EcsFilter](https://leopotam.github.io/ecs/filter-gen.html) классов и заменить содержимое файла `EcsFilter.cs`.

### Я хочу добавить реактивщины и обрабатывать события изменения фильтров. Как я могу это сделать?

> **ВАЖНО!** Так делать не рекомендуется из-за падения производительности.

Для активации этого функционала следует добавить `LEOECS_FILTER_EVENTS` в список директив комплятора, а затем - добавить слушатель событий:
```c#
class CustomListener: IEcsFilterListener {
    public void OnEntityAdded (in EcsEntity entity) {
        // Сущность добавлена в фильтр.
    }
    
    public void OnEntityRemoved (in EcsEntity entity) {
        // Сущность удалена из фильтра.
    }
}

class MySystem : IEcsInitSystem, IEcsDestroySystem {
    readonly EcsFilter<Component1> _filter = null;
    readonly CustomListener _listener = new CustomListener ();
    public void Init () {
        // Подписка слушателя на события фильтра.
        _filter.AddListener (_listener);
    }
    public void Destroy () {
        // Отписка слушателя от событий фильтра.
        _filter.RemoveListener (_listener);
    }
}
``` 