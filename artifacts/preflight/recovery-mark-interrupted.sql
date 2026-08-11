INSERT INTO wk.action_phase(action_phase_id,action_id,phase,payload)
SELECT gen_random_uuid(), a.action_id, 'interrupted', '{"recovery":"provider reobservation required; live process continuity not claimed"}'::jsonb
FROM wk.action_attempt a
WHERE EXISTS (SELECT 1 FROM wk.action_phase p WHERE p.action_id=a.action_id AND p.phase='dispatched')
  AND NOT EXISTS (SELECT 1 FROM wk.transition_episode e WHERE e.action_id=a.action_id)
  AND NOT EXISTS (SELECT 1 FROM wk.action_phase p WHERE p.action_id=a.action_id AND p.phase='interrupted');