using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Pool {
    public class ParticleSystemPool {
        private readonly ParticleSystem _prefab;
        private readonly int _startCount;
        private readonly int _maxInstancesCount;
        private readonly Stack<ParticleSystem> _freeParticles = new Stack<ParticleSystem>();
        private readonly Queue<ParticleSystem> _usedParticles = new Queue<ParticleSystem>();
        private int _instancesCount;
        private Transform _parent;

        private ParticleSystemPool(ParticleSystem prefab, int startCount, int maxInstancesCount) {
            _prefab = prefab;
            _startCount = startCount;
            _maxInstancesCount = maxInstancesCount;
        }

        public static ParticleSystemPool Create(ParticleSystem prefab, int startCount) {
            var result = new ParticleSystemPool(prefab, startCount, 500);
            result.CreateParent();
            result.Prewarm();
            return result;
        }

        private void CreateParent() {
            var parentGO = new GameObject("effects_parent");
            _parent = parentGO.transform;
        }

        private void Prewarm() {
            for (int i = 0; i < _startCount; i++) {
                var instance = CreateNew();
                instance.gameObject.SetActive(false);
                _freeParticles.Push(instance);
            }
        }

        private ParticleSystem CreateNew() {
            _instancesCount++;
            return Object.Instantiate(_prefab, _parent);
        }

        public void PlayParticle(float3 position) {
            ParticleSystem particle = null;
            if (_usedParticles.Count > 0 && !_usedParticles.Peek().isPlaying) {
                particle = _usedParticles.Dequeue();
            }
            else if (_freeParticles.Count > 0) {
                particle = _freeParticles.Pop();
            } else {
                if (_instancesCount >= _maxInstancesCount) {
                    return;
                }
                particle = CreateNew();
            }

            particle.transform.localPosition = new Vector3(position.x, position.y, position.z);
            particle.gameObject.SetActive(true);
            particle.Play();
            _usedParticles.Enqueue(particle);
        }
    }
}
