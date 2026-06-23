using System;
using System.Collections.Generic;

namespace Tower.Core
{
    /// <summary>
    /// 아주 단순한 정적 서비스 로케이터. DI 프레임워크 없이 솔로/소규모 프로젝트에
    /// 충분한 수준으로, 타입을 키로 단일 인스턴스를 등록/조회한다.
    /// (마이그레이션 M0 골격 — 아직 게임 로직에 연결되지 않음)
    /// </summary>
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            return services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = null; return false;
        }

        public static void Unregister<T>() where T : class => services.Remove(typeof(T));

        /// <summary>씬/판 재시작 시 전체 초기화용.</summary>
        public static void Clear() => services.Clear();
    }
}
